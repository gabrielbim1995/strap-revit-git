using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using StrapRevit.Core.IFC;
using StrapRevit.Core.Mapping;
using StrapRevit.Core.Parameters;
using StrapRevit.Views;

namespace StrapRevit.Core
{
    /// <summary>
    /// Processador principal de arquivos IFC do STRAP
    /// </summary>
    public class IFCProcessor
    {
        private readonly Document _doc;
        private readonly ElementMapper _mapper;
        private readonly ParameterManager _paramManager;
        private readonly Dictionary<string, ElementId> _guidToElementId;
        private readonly List<string> _warnings;

        public IFCProcessor(Document doc)
        {
            _doc = doc;
            _mapper = new ElementMapper(doc);
            _paramManager = new ParameterManager(doc);
            _guidToElementId = new Dictionary<string, ElementId>();
            _warnings = new List<string>();
        }

        /// <summary>
        /// Processa arquivo IFC e importa/atualiza elementos no Revit
        /// </summary>
        public ImportResult ProcessIFCFile(string ifcPath, ImportConfiguration config, ImportProgressForm progressForm)
        {
            var result = new ImportResult { FileName = Path.GetFileName(ifcPath) };
            var stopwatch = Stopwatch.StartNew();

            try
            {
                progressForm.UpdateProgress("Lendo arquivo IFC...", 0);

                // Parse do arquivo IFC
                var parser = new IFCParser();
                var ifcModel = parser.ParseFile(ifcPath);

                if (ifcModel == null || !ifcModel.Elements.Any())
                {
                    result.Success = false;
                    result.Warnings.Add("Arquivo IFC vazio ou inválido");
                    return result;
                }

                progressForm.UpdateProgress($"Encontrados {ifcModel.Elements.Count} elementos", 10);

                // Carregar mapeamento existente
                LoadExistingGuidMapping();

                // Processar elementos em transação
                using (var transGroup = new TransactionGroup(_doc, "Importar IFC do STRAP"))
                {
                    transGroup.Start();

                    try
                    {
                        // Preparar ambiente (níveis, materiais, tipos)
                        PrepareEnvironment(ifcModel, config, progressForm);

                        // Processar cada tipo de elemento
                        ProcessBeams(ifcModel.Beams, config, result, progressForm);
                        ProcessColumns(ifcModel.Columns, config, result, progressForm);
                        ProcessSlabs(ifcModel.Slabs, config, result, progressForm);
                        ProcessFoundations(ifcModel.Foundations, config, result, progressForm);

                        // Marcar elementos órfãos
                        if (config.UpdateExisting)
                        {
                            MarkOrphanElements(ifcModel, result);
                        }

                        // Salvar informações de rastreabilidade
                        SaveTrackingInfo(ifcPath);

                        transGroup.Assimilate();
                        result.Success = true;
                    }
                    catch (Exception ex)
                    {
                        transGroup.RollBack();
                        result.Success = false;
                        result.Warnings.Add($"Erro no processamento: {ex.Message}");
                    }
                }

                result.Warnings.AddRange(_warnings);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Warnings.Add($"Erro ao ler arquivo IFC: {ex.Message}");
            }
            finally
            {
                stopwatch.Stop();
                result.ProcessingTime = stopwatch.Elapsed.TotalSeconds;
            }

            return result;
        }

        /// <summary>
        /// Carrega mapeamento GUID existente do projeto
        /// </summary>
        private void LoadExistingGuidMapping()
        {
            _guidToElementId.Clear();

            var collector = new FilteredElementCollector(_doc)
                .WhereElementIsNotElementType();

            foreach (var element in collector)
            {
                var guid = _paramManager.GetIFCGuid(element);
                if (!string.IsNullOrEmpty(guid))
                {
                    _guidToElementId[guid] = element.Id;
                }
            }
        }

        /// <summary>
        /// Prepara ambiente criando níveis, materiais e tipos necessários
        /// </summary>
        private void PrepareEnvironment(IFCModel model, ImportConfiguration config, ImportProgressForm progressForm)
        {
            using (var trans = new Transaction(_doc, "Preparar ambiente"))
            {
                trans.Start();

                progressForm.UpdateProgress("Criando níveis necessários...", 20);

                // Criar níveis se necessário
                if (config.CreateLevels)
                {
                    var levelCreator = new LevelCreator(_doc);
                    var createdLevels = levelCreator.CreateRequiredLevels(model.RequiredLevels);
                    _mapper.UpdateLevelMapping(createdLevels);
                }

                progressForm.UpdateProgress("Criando materiais necessários...", 30);

                // Criar materiais se necessário
                if (config.CreateMaterials)
                {
                    var materialCreator = new MaterialCreator(_doc);
                    var createdMaterials = materialCreator.CreateRequiredMaterials(model.RequiredMaterials);
                    _mapper.UpdateMaterialMapping(createdMaterials);
                }

                trans.Commit();
            }
        }

        /// <summary>
        /// Processa vigas do IFC
        /// </summary>
        private void ProcessBeams(List<IFCBeam> beams, ImportConfiguration config, ImportResult result, ImportProgressForm progressForm)
        {
            if (!beams.Any()) return;

            using (var trans = new Transaction(_doc, "Processar vigas"))
            {
                trans.Start();

                int processed = 0;
                foreach (var ifcBeam in beams)
                {
                    try
                    {
                        progressForm.UpdateProgress($"Processando vigas... ({processed}/{beams.Count})", 
                            40 + (processed * 15 / beams.Count));

                        if (_guidToElementId.TryGetValue(ifcBeam.GlobalId, out ElementId existingId))
                        {
                            // Atualizar viga existente
                            UpdateBeam(existingId, ifcBeam, config);
                            result.UpdatedBeams++;
                        }
                        else
                        {
                            // Criar nova viga
                            CreateBeam(ifcBeam, config);
                            result.CreatedBeams++;
                        }

                        processed++;
                    }
                    catch (Exception ex)
                    {
                        _warnings.Add($"Erro ao processar viga {ifcBeam.Name}: {ex.Message}");
                    }
                }

                result.BeamCount = beams.Count;
                trans.Commit();
            }
        }

        /// <summary>
        /// Cria nova viga no Revit
        /// </summary>
        private void CreateBeam(IFCBeam ifcBeam, ImportConfiguration config)
        {
            // Obter tipo de viga apropriado
            var beamType = _mapper.GetBeamType(ifcBeam.Profile, ifcBeam.Material);
            if (beamType == null)
            {
                _warnings.Add($"Nenhum tipo de viga disponível para {ifcBeam.Name} - elemento não criado");
                return;
            }

            // Obter nível
            var level = _mapper.GetLevel(ifcBeam.Level);
            if (level == null)
            {
                _warnings.Add($"Nível não encontrado para viga {ifcBeam.Name}");
                return;
            }

            // Converter coordenadas
            var startPoint = ConvertPoint(ifcBeam.StartPoint);
            var endPoint = ConvertPoint(ifcBeam.EndPoint);

            // Criar linha
            var line = Line.CreateBound(startPoint, endPoint);

            // Criar viga
            var beam = _doc.Create.NewFamilyInstance(
                line,
                beamType as FamilySymbol,
                level,
                StructuralType.Beam
            ) as FamilyInstance;

            if (beam != null)
            {
                // Definir parâmetros
                _paramManager.SetIFCParameters(beam, ifcBeam);
                
                // Verificar se foi usado fallback e registrar
                if (beamType.Name.Contains("[FALLBACK:"))
                {
                    var parts = beamType.Name.Split(new[] { "[FALLBACK: ", "]" }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        var intendedFamily = parts[1];
                        var usedFamily = beamType.Family.Name;
                        _paramManager.SetFallbackInfo(beam, intendedFamily, usedFamily);
                        _warnings.Add($"Viga {ifcBeam.Name}: Usado fallback {usedFamily} no lugar de {intendedFamily}");
                    }
                }
                
                // Aplicar rotação se necessário
                if (Math.Abs(ifcBeam.Rotation) > 0.001)
                {
                    var rotationAxis = Line.CreateBound(startPoint, endPoint);
                    ElementTransformUtils.RotateElement(_doc, beam.Id, rotationAxis, ifcBeam.Rotation);
                }

                // Definir material
                var material = _mapper.GetMaterial(ifcBeam.Material);
                if (material != null)
                {
                    beam.get_Parameter(BuiltInParameter.STRUCTURAL_MATERIAL_PARAM)?.Set(material.Id);
                }

                // Definir fase e workset
                SetPhaseAndWorkset(beam, config);

                // Registrar mapeamento
                _guidToElementId[ifcBeam.GlobalId] = beam.Id;
            }
        }

        /// <summary>
        /// Atualiza viga existente
        /// </summary>
        private void UpdateBeam(ElementId beamId, IFCBeam ifcBeam, ImportConfiguration config)
        {
            var beam = _doc.GetElement(beamId) as FamilyInstance;
            if (beam == null) return;

            // Verificar se tipo mudou
            var newType = _mapper.GetBeamType(ifcBeam.Profile, ifcBeam.Material);
            if (newType != null && beam.Symbol.Id != newType.Id)
            {
                beam.Symbol = newType as FamilySymbol;
            }

            // Atualizar localização
            var curve = (beam.Location as LocationCurve)?.Curve as Line;
            if (curve != null)
            {
                var newStart = ConvertPoint(ifcBeam.StartPoint);
                var newEnd = ConvertPoint(ifcBeam.EndPoint);

                if (!curve.GetEndPoint(0).IsAlmostEqualTo(newStart) || 
                    !curve.GetEndPoint(1).IsAlmostEqualTo(newEnd))
                {
                    var newLine = Line.CreateBound(newStart, newEnd);
                    (beam.Location as LocationCurve).Curve = newLine;
                }
            }

            // Atualizar parâmetros
            _paramManager.UpdateIFCParameters(beam, ifcBeam);
        }

        /// <summary>
        /// Processa pilares do IFC
        /// </summary>
        private void ProcessColumns(List<IFCColumn> columns, ImportConfiguration config, ImportResult result, ImportProgressForm progressForm)
        {
            if (!columns.Any()) return;

            using (var trans = new Transaction(_doc, "Processar pilares"))
            {
                trans.Start();

                int processed = 0;
                foreach (var ifcColumn in columns)
                {
                    try
                    {
                        progressForm.UpdateProgress($"Processando pilares... ({processed}/{columns.Count})", 
                            55 + (processed * 15 / columns.Count));

                        if (_guidToElementId.TryGetValue(ifcColumn.GlobalId, out ElementId existingId))
                        {
                            UpdateColumn(existingId, ifcColumn, config);
                            result.UpdatedColumns++;
                        }
                        else
                        {
                            CreateColumn(ifcColumn, config);
                            result.CreatedColumns++;
                        }

                        processed++;
                    }
                    catch (Exception ex)
                    {
                        _warnings.Add($"Erro ao processar pilar {ifcColumn.Name}: {ex.Message}");
                    }
                }

                result.ColumnCount = columns.Count;
                trans.Commit();
            }
        }

        /// <summary>
        /// Cria novo pilar no Revit
        /// </summary>
        private void CreateColumn(IFCColumn ifcColumn, ImportConfiguration config)
        {
            var columnType = _mapper.GetColumnType(ifcColumn.Profile, ifcColumn.Material);
            if (columnType == null)
            {
                _warnings.Add($"Nenhum tipo de pilar disponível para {ifcColumn.Name} - elemento não criado");
                return;
            }

            var baseLevel = _mapper.GetLevel(ifcColumn.BaseLevel);
            var topLevel = _mapper.GetLevel(ifcColumn.TopLevel);
            
            if (baseLevel == null || topLevel == null)
            {
                _warnings.Add($"Níveis não encontrados para pilar {ifcColumn.Name}");
                return;
            }

            var location = ConvertPoint(ifcColumn.Location);

            var column = _doc.Create.NewFamilyInstance(
                location,
                columnType as FamilySymbol,
                baseLevel,
                StructuralType.Column
            ) as FamilyInstance;

            if (column != null)
            {
                // Definir topo
                column.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM)?.Set(topLevel.Id);
                column.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM)?.Set(ifcColumn.TopOffset);
                column.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM)?.Set(ifcColumn.BaseOffset);

                // Aplicar rotação
                if (Math.Abs(ifcColumn.Rotation) > 0.001)
                {
                    var axis = Line.CreateBound(location, location + XYZ.BasisZ);
                    ElementTransformUtils.RotateElement(_doc, column.Id, axis, ifcColumn.Rotation);
                }

                _paramManager.SetIFCParameters(column, ifcColumn);
                
                // Verificar se foi usado fallback e registrar
                if (columnType.Name.Contains("[FALLBACK:"))
                {
                    var parts = columnType.Name.Split(new[] { "[FALLBACK: ", "]" }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        var intendedFamily = parts[1];
                        var usedFamily = columnType.Family.Name;
                        _paramManager.SetFallbackInfo(column, intendedFamily, usedFamily);
                        _warnings.Add($"Pilar {ifcColumn.Name}: Usado fallback {usedFamily} no lugar de {intendedFamily}");
                    }
                }
                
                SetPhaseAndWorkset(column, config);
                _guidToElementId[ifcColumn.GlobalId] = column.Id;
            }
        }

        /// <summary>
        /// Processa lajes do IFC
        /// </summary>
        private void ProcessSlabs(List<IFCSlab> slabs, ImportConfiguration config, ImportResult result, ImportProgressForm progressForm)
        {
            if (!slabs.Any()) return;

            using (var trans = new Transaction(_doc, "Processar lajes"))
            {
                trans.Start();

                int processed = 0;
                foreach (var ifcSlab in slabs)
                {
                    try
                    {
                        progressForm.UpdateProgress($"Processando lajes... ({processed}/{slabs.Count})", 
                            70 + (processed * 15 / slabs.Count));

                        if (_guidToElementId.TryGetValue(ifcSlab.GlobalId, out ElementId existingId))
                        {
                            UpdateSlab(existingId, ifcSlab, config);
                            result.UpdatedSlabs++;
                        }
                        else
                        {
                            CreateSlab(ifcSlab, config);
                            result.CreatedSlabs++;
                        }

                        processed++;
                    }
                    catch (Exception ex)
                    {
                        _warnings.Add($"Erro ao processar laje {ifcSlab.Name}: {ex.Message}");
                    }
                }

                result.SlabCount = slabs.Count;
                trans.Commit();
            }
        }

        /// <summary>
        /// Cria nova laje no Revit
        /// </summary>
        private void CreateSlab(IFCSlab ifcSlab, ImportConfiguration config)
        {
            var floorType = _mapper.GetFloorType(ifcSlab.Thickness, ifcSlab.Material);
            if (floorType == null)
            {
                _warnings.Add($"Tipo de laje não encontrado para {ifcSlab.Name}");
                return;
            }

            var level = _mapper.GetLevel(ifcSlab.Level);
            if (level == null)
            {
                _warnings.Add($"Nível não encontrado para laje {ifcSlab.Name}");
                return;
            }

            // Converter contorno
            var curveArray = new CurveArray();
            foreach (var point in ifcSlab.BoundaryPoints)
            {
                var p1 = ConvertPoint(point);
                var p2 = ConvertPoint(ifcSlab.BoundaryPoints[(ifcSlab.BoundaryPoints.IndexOf(point) + 1) % ifcSlab.BoundaryPoints.Count]);
                curveArray.Append(Line.CreateBound(p1, p2));
            }

            var floor = Floor.Create(_doc, new List<CurveLoop> { CurveLoop.Create(curveArray.Cast<Curve>().ToList()) }, floorType.Id, level.Id);

            if (floor != null)
            {
                // Definir offset
                floor.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM)?.Set(ifcSlab.Offset);

                _paramManager.SetIFCParameters(floor, ifcSlab);
                SetPhaseAndWorkset(floor, config);
                _guidToElementId[ifcSlab.GlobalId] = floor.Id;
            }
        }

        /// <summary>
        /// Processa fundações do IFC
        /// </summary>
        private void ProcessFoundations(List<IFCFoundation> foundations, ImportConfiguration config, ImportResult result, ImportProgressForm progressForm)
        {
            if (!foundations.Any()) return;

            using (var trans = new Transaction(_doc, "Processar fundações"))
            {
                trans.Start();

                int processed = 0;
                foreach (var ifcFoundation in foundations)
                {
                    try
                    {
                        progressForm.UpdateProgress($"Processando fundações... ({processed}/{foundations.Count})", 
                            85 + (processed * 10 / foundations.Count));

                        if (_guidToElementId.TryGetValue(ifcFoundation.GlobalId, out ElementId existingId))
                        {
                            UpdateFoundation(existingId, ifcFoundation, config);
                            result.UpdatedFoundations++;
                        }
                        else
                        {
                            CreateFoundation(ifcFoundation, config);
                            result.CreatedFoundations++;
                        }

                        processed++;
                    }
                    catch (Exception ex)
                    {
                        _warnings.Add($"Erro ao processar fundação {ifcFoundation.Name}: {ex.Message}");
                    }
                }

                result.FoundationCount = foundations.Count;
                trans.Commit();
            }
        }

        /// <summary>
        /// Marca elementos órfãos (que não existem mais no IFC)
        /// </summary>
        private void MarkOrphanElements(IFCModel model, ImportResult result)
        {
            using (var trans = new Transaction(_doc, "Marcar elementos órfãos"))
            {
                trans.Start();

                var currentGuids = new HashSet<string>(model.Elements.Select(e => e.GlobalId));

                foreach (var kvp in _guidToElementId)
                {
                    if (!currentGuids.Contains(kvp.Key))
                    {
                        var element = _doc.GetElement(kvp.Value);
                        if (element != null)
                        {
                            _paramManager.MarkAsOrphan(element);
                            result.OrphanCount++;
                        }
                    }
                }

                trans.Commit();
            }
        }

        /// <summary>
        /// Converte ponto do IFC para coordenadas do Revit
        /// </summary>
        private XYZ ConvertPoint(IFCPoint point)
        {
            // Converter de milímetros para pés (unidade interna do Revit)
            const double mmToFeet = 1.0 / 304.8;
            return new XYZ(
                point.X * mmToFeet,
                point.Y * mmToFeet,
                point.Z * mmToFeet
            );
        }

        /// <summary>
        /// Define fase e workset do elemento
        /// </summary>
        private void SetPhaseAndWorkset(Element element, ImportConfiguration config)
        {
            // Definir fase
            if (config.DefaultPhaseId != ElementId.InvalidElementId)
            {
                element.get_Parameter(BuiltInParameter.PHASE_CREATED)?.Set(config.DefaultPhaseId);
            }

            // Definir workset
            if (_doc.IsWorkshared && config.DefaultWorksetId != WorksetId.InvalidWorksetId.IntegerValue)
            {
                var wsparam = element.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM);
                wsparam?.Set(config.DefaultWorksetId);
            }
        }

        /// <summary>
        /// Atualiza pilar existente
        /// </summary>
        private void UpdateColumn(ElementId columnId, IFCColumn ifcColumn, ImportConfiguration config)
        {
            var column = _doc.GetElement(columnId) as FamilyInstance;
            if (column == null) return;

            // Verificar se tipo mudou
            var newType = _mapper.GetColumnType(ifcColumn.Profile, ifcColumn.Material);
            if (newType != null && column.Symbol.Id != newType.Id)
            {
                column.Symbol = newType as FamilySymbol;
            }

            // Atualizar localização
            var location = column.Location as LocationPoint;
            if (location != null)
            {
                var newPoint = ConvertPoint(ifcColumn.Location);
                if (!location.Point.IsAlmostEqualTo(newPoint))
                {
                    location.Point = newPoint;
                }
            }

            // Atualizar níveis e offsets
            var baseLevel = _mapper.GetLevel(ifcColumn.BaseLevel);
            var topLevel = _mapper.GetLevel(ifcColumn.TopLevel);
            
            if (baseLevel != null)
            {
                column.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_PARAM)?.Set(baseLevel.Id);
                column.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM)?.Set(ifcColumn.BaseOffset);
            }
            
            if (topLevel != null)
            {
                column.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM)?.Set(topLevel.Id);
                column.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM)?.Set(ifcColumn.TopOffset);
            }

            // Atualizar parâmetros IFC
            _paramManager.UpdateIFCParameters(column, ifcColumn);
        }

        /// <summary>
        /// Atualiza laje existente
        /// </summary>
        private void UpdateSlab(ElementId slabId, IFCSlab ifcSlab, ImportConfiguration config)
        {
            var floor = _doc.GetElement(slabId) as Floor;
            if (floor == null) return;

            // Verificar se tipo mudou
            var newType = _mapper.GetFloorType(ifcSlab.Thickness, ifcSlab.Material);
            if (newType != null && floor.FloorType.Id != newType.Id)
            {
                floor.FloorType = newType;
            }

            // Atualizar offset
            floor.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM)?.Set(ifcSlab.Offset);

            // Atualizar parâmetros IFC
            _paramManager.UpdateIFCParameters(floor, ifcSlab);
        }

        /// <summary>
        /// Cria nova fundação no Revit
        /// </summary>
        private void CreateFoundation(IFCFoundation ifcFoundation, ImportConfiguration config)
        {
            // Determinar tipo de fundação
            FamilySymbol foundationType = null;
            
            switch (ifcFoundation.FoundationType)
            {
                case FoundationType.Isolated:
                    foundationType = _mapper.GetIsolatedFoundationType(ifcFoundation.Dimensions, ifcFoundation.Material);
                    break;
                case FoundationType.Strip:
                    foundationType = _mapper.GetStripFoundationType(ifcFoundation.Width, ifcFoundation.Height, ifcFoundation.Material);
                    break;
                case FoundationType.Mat:
                    // Fundação em radier - usar laje estrutural
                    CreateFoundationSlab(ifcFoundation, config);
                    return;
            }

            if (foundationType == null)
            {
                _warnings.Add($"Tipo de fundação não encontrado para {ifcFoundation.Name}");
                return;
            }

            var level = _mapper.GetLevel(ifcFoundation.Level);
            if (level == null)
            {
                _warnings.Add($"Nível não encontrado para fundação {ifcFoundation.Name}");
                return;
            }

            Element foundation = null;

            if (ifcFoundation.FoundationType == FoundationType.Isolated)
            {
                // Fundação isolada (sapata)
                var location = ConvertPoint(ifcFoundation.Location);
                foundation = _doc.Create.NewFamilyInstance(
                    location,
                    foundationType,
                    level,
                    StructuralType.Footing
                );
            }
            else if (ifcFoundation.FoundationType == FoundationType.Strip)
            {
                // Fundação corrida
                if (ifcFoundation.Path.Count > 0)
                {
                    var curve = ConvertCurve(ifcFoundation.Path[0]);
                    foundation = _doc.Create.NewFamilyInstance(
                        curve,
                        foundationType,
                        level,
                        StructuralType.Footing
                    );
                }
            }

            if (foundation != null)
            {
                _paramManager.SetIFCParameters(foundation, ifcFoundation);
                SetPhaseAndWorkset(foundation, config);
                _guidToElementId[ifcFoundation.GlobalId] = foundation.Id;
            }
        }

        /// <summary>
        /// Cria fundação tipo radier como laje estrutural
        /// </summary>
        private void CreateFoundationSlab(IFCFoundation ifcFoundation, ImportConfiguration config)
        {
            var floorType = _mapper.GetStructuralFloorType(ifcFoundation.Height, ifcFoundation.Material);
            if (floorType == null)
            {
                _warnings.Add($"Tipo de laje estrutural não encontrado para fundação {ifcFoundation.Name}");
                return;
            }

            var level = _mapper.GetLevel(ifcFoundation.Level);
            if (level == null) return;

            // Converter contorno
            var curveArray = new CurveArray();
            foreach (var curve in ifcFoundation.Boundary)
            {
                curveArray.Append(ConvertCurve(curve));
            }

            var foundation = Floor.Create(_doc, new List<CurveLoop> { CurveLoop.Create(curveArray.Cast<Curve>().ToList()) }, floorType.Id, level.Id, true, null, 0); // true = estrutural

            if (foundation != null)
            {
                // Marcar como fundação
                _paramManager.SetFoundationType(foundation, "Radier");
                _paramManager.SetIFCParameters(foundation, ifcFoundation);
                SetPhaseAndWorkset(foundation, config);
                _guidToElementId[ifcFoundation.GlobalId] = foundation.Id;
            }
        }

        /// <summary>
        /// Atualiza fundação existente
        /// </summary>
        private void UpdateFoundation(ElementId foundationId, IFCFoundation ifcFoundation, ImportConfiguration config)
        {
            var element = _doc.GetElement(foundationId);
            if (element == null) return;

            // Atualizar parâmetros
            _paramManager.UpdateIFCParameters(element, ifcFoundation);

            // Se for fundação isolada, atualizar localização
            if (element is FamilyInstance fi && element.Location is LocationPoint lp)
            {
                var newLocation = ConvertPoint(ifcFoundation.Location);
                if (!lp.Point.IsAlmostEqualTo(newLocation))
                {
                    lp.Point = newLocation;
                }
            }
        }

        /// <summary>
        /// Converte curva do IFC para Revit
        /// </summary>
        private Curve ConvertCurve(IFCCurve ifcCurve)
        {
            if (ifcCurve.IsLine)
            {
                return Line.CreateBound(
                    ConvertPoint(ifcCurve.StartPoint),
                    ConvertPoint(ifcCurve.EndPoint)
                );
            }
            else if (ifcCurve.IsArc)
            {
                return Arc.Create(
                    ConvertPoint(ifcCurve.StartPoint),
                    ConvertPoint(ifcCurve.EndPoint),
                    ConvertPoint(ifcCurve.MidPoint)
                );
            }

            // Fallback para linha
            return Line.CreateBound(
                ConvertPoint(ifcCurve.StartPoint),
                ConvertPoint(ifcCurve.EndPoint)
            );
        }

        /// <summary>
        /// Salva informações de rastreabilidade
        /// </summary>
        private void SaveTrackingInfo(string ifcPath)
        {
            using (var trans = new Transaction(_doc, "Salvar informações de rastreabilidade"))
            {
                trans.Start();

                // Salvar no ProjectInfo
                var projectInfo = _doc.ProjectInformation;
                _paramManager.SetLastImportInfo(projectInfo, ifcPath, DateTime.Now);

                trans.Commit();
            }
        }
    }

    /// <summary>
    /// Resultado da importação
    /// </summary>
    public class ImportResult
    {
        public bool Success { get; set; }
        public string FileName { get; set; }
        public double ProcessingTime { get; set; }
        
        public int BeamCount { get; set; }
        public int CreatedBeams { get; set; }
        public int UpdatedBeams { get; set; }
        
        public int ColumnCount { get; set; }
        public int CreatedColumns { get; set; }
        public int UpdatedColumns { get; set; }
        
        public int SlabCount { get; set; }
        public int CreatedSlabs { get; set; }
        public int UpdatedSlabs { get; set; }
        
        public int FoundationCount { get; set; }
        public int CreatedFoundations { get; set; }
        public int UpdatedFoundations { get; set; }
        
        public int OrphanCount { get; set; }
        public int MaterialsCreated { get; set; }
        public int LevelsCreated { get; set; }
        public int TypesCreated { get; set; }
        
        public List<string> Warnings { get; set; } = new List<string>();

        public int TotalElements => BeamCount + ColumnCount + SlabCount + FoundationCount;
        public int TotalCreated => CreatedBeams + CreatedColumns + CreatedSlabs + CreatedFoundations;
        public int TotalUpdated => UpdatedBeams + UpdatedColumns + UpdatedSlabs + UpdatedFoundations;
    }

    /// <summary>
    /// Configuração de importação
    /// </summary>
    public class ImportConfiguration
    {
        public bool CreateLevels { get; set; } = true;
        public bool CreateMaterials { get; set; } = true;
        public bool UpdateExisting { get; set; } = true;
        public ElementId DefaultPhaseId { get; set; } = ElementId.InvalidElementId;
        public int DefaultWorksetId { get; set; } = WorksetId.InvalidWorksetId.IntegerValue;
        public string FamilyLoadPath { get; set; }
    }
}
