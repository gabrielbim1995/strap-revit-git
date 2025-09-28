using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using StrapRevit.Core.Extensions;

namespace StrapRevit.Core.IFC
{
    /// <summary>
    /// Parser de arquivos IFC
    /// </summary>
    public class IFCParser
    {
        private Dictionary<int, IFCEntity> _entities;
        private Dictionary<string, IFCEntity> _entitiesByGuid;

        public IFCParser()
        {
            _entities = new Dictionary<int, IFCEntity>();
            _entitiesByGuid = new Dictionary<string, IFCEntity>();
        }

        /// <summary>
        /// Faz o parse do arquivo IFC
        /// </summary>
        public IFCModel ParseFile(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Arquivo IFC não encontrado: {filePath}");

            var model = new IFCModel();
            
            try
            {
                // Ler todas as linhas do arquivo
                var lines = File.ReadAllLines(filePath, Encoding.UTF8);
                
                // Processar entidades
                ProcessEntities(lines);
                
                // Extrair elementos estruturais
                ExtractStructuralElements(model);
                
                // Extrair informações complementares
                ExtractProjectInfo(model);
                ExtractRequiredLevels(model);
                ExtractRequiredMaterials(model);
            }
            catch (Exception ex)
            {
                throw new Exception($"Erro ao processar arquivo IFC: {ex.Message}", ex);
            }

            return model;
        }

        /// <summary>
        /// Processa as entidades do arquivo IFC
        /// </summary>
        private void ProcessEntities(string[] lines)
        {
            var entityPattern = @"^#(\d+)\s*=\s*(\w+)\s*\((.*)\)\s*;";
            var currentEntity = new StringBuilder();
            
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("ISO-10303"))
                    continue;

                currentEntity.Append(line.Trim());

                // Verificar se a entidade está completa
                if (line.TrimEnd().EndsWith(";"))
                {
                    var entityString = currentEntity.ToString();
                    var match = Regex.Match(entityString, entityPattern, RegexOptions.Singleline);
                    
                    if (match.Success)
                    {
                        var id = int.Parse(match.Groups[1].Value);
                        var type = match.Groups[2].Value;
                        var attributes = match.Groups[3].Value;
                        
                        var entity = new IFCEntity
                        {
                            Id = id,
                            Type = type,
                            Attributes = ParseAttributes(attributes)
                        };
                        
                        _entities[id] = entity;
                        
                        // Se for uma entidade com GUID, indexar também por GUID
                        if (entity.Attributes.Count > 0 && entity.Attributes[0] is string guid)
                        {
                            _entitiesByGuid[guid.Trim('\'')] = entity;
                        }
                    }
                    
                    currentEntity.Clear();
                }
            }
        }

        /// <summary>
        /// Faz o parse dos atributos de uma entidade
        /// </summary>
        private List<object> ParseAttributes(string attributesString)
        {
            var attributes = new List<object>();
            var current = new StringBuilder();
            int depth = 0;
            bool inString = false;
            
            for (int i = 0; i < attributesString.Length; i++)
            {
                char c = attributesString[i];
                
                if (c == '\'' && (i == 0 || attributesString[i - 1] != '\\'))
                {
                    inString = !inString;
                }
                
                if (!inString)
                {
                    if (c == '(') depth++;
                    else if (c == ')') depth--;
                    else if (c == ',' && depth == 0)
                    {
                        attributes.Add(ParseAttribute(current.ToString().Trim()));
                        current.Clear();
                        continue;
                    }
                }
                
                current.Append(c);
            }
            
            if (current.Length > 0)
            {
                attributes.Add(ParseAttribute(current.ToString().Trim()));
            }
            
            return attributes;
        }

        /// <summary>
        /// Faz o parse de um atributo individual
        /// </summary>
        private object ParseAttribute(string attribute)
        {
            if (string.IsNullOrEmpty(attribute) || attribute == "$")
                return null;
                
            if (attribute.StartsWith("'") && attribute.EndsWith("'"))
                return attribute.Substring(1, attribute.Length - 2);
                
            if (attribute.StartsWith("#"))
            {
                if (int.TryParse(attribute.Substring(1), out int refId))
                    return refId;
            }
            
            if (double.TryParse(attribute, out double number))
                return number;
                
            if (attribute.StartsWith("(") && attribute.EndsWith(")"))
                return ParseAttributes(attribute.Substring(1, attribute.Length - 2));
                
            return attribute;
        }

        /// <summary>
        /// Extrai elementos estruturais do modelo IFC
        /// </summary>
        private void ExtractStructuralElements(IFCModel model)
        {
            foreach (var entity in _entities.Values)
            {
                switch (entity.Type.ToUpper())
                {
                    case "IFCBEAM":
                        var beam = ExtractBeam(entity);
                        if (beam != null)
                        {
                            model.Beams.Add(beam);
                            model.Elements.Add(beam);
                        }
                        break;
                        
                    case "IFCCOLUMN":
                        var column = ExtractColumn(entity);
                        if (column != null)
                        {
                            model.Columns.Add(column);
                            model.Elements.Add(column);
                        }
                        break;
                        
                    case "IFCSLAB":
                        var slab = ExtractSlab(entity);
                        if (slab != null)
                        {
                            model.Slabs.Add(slab);
                            model.Elements.Add(slab);
                        }
                        break;
                        
                    case "IFCFOOTING":
                        var foundation = ExtractFoundation(entity);
                        if (foundation != null)
                        {
                            model.Foundations.Add(foundation);
                            model.Elements.Add(foundation);
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// Extrai informações de uma viga
        /// </summary>
        private IFCBeam ExtractBeam(IFCEntity entity)
        {
            try
            {
                var beam = new IFCBeam
                {
                    GlobalId = GetString(entity, 0),
                    Name = GetString(entity, 2),
                    Description = GetString(entity, 3)
                };

                // Obter geometria e propriedades
                ExtractPlacement(entity, beam);
                ExtractProfile(entity, beam);
                ExtractMaterial(entity, beam);
                ExtractLevel(entity, beam);

                return beam;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Extrai informações de um pilar
        /// </summary>
        private IFCColumn ExtractColumn(IFCEntity entity)
        {
            try
            {
                var column = new IFCColumn
                {
                    GlobalId = GetString(entity, 0),
                    Name = GetString(entity, 2),
                    Description = GetString(entity, 3)
                };

                ExtractPlacement(entity, column);
                ExtractProfile(entity, column);
                ExtractMaterial(entity, column);
                ExtractColumnLevels(entity, column);

                return column;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Extrai informações de uma laje
        /// </summary>
        private IFCSlab ExtractSlab(IFCEntity entity)
        {
            try
            {
                var slab = new IFCSlab
                {
                    GlobalId = GetString(entity, 0),
                    Name = GetString(entity, 2),
                    Description = GetString(entity, 3)
                };

                ExtractSlabGeometry(entity, slab);
                ExtractMaterial(entity, slab);
                ExtractLevel(entity, slab);

                return slab;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Extrai informações de uma fundação
        /// </summary>
        private IFCFoundation ExtractFoundation(IFCEntity entity)
        {
            try
            {
                var foundation = new IFCFoundation
                {
                    GlobalId = GetString(entity, 0),
                    Name = GetString(entity, 2),
                    Description = GetString(entity, 3)
                };

                ExtractFoundationGeometry(entity, foundation);
                ExtractMaterial(entity, foundation);
                ExtractLevel(entity, foundation);

                return foundation;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Extrai posicionamento do elemento
        /// </summary>
        private void ExtractPlacement(IFCEntity entity, IFCElement element)
        {
            var placementRef = GetReference(entity, 5);
            if (placementRef.HasValue && _entities.TryGetValue(placementRef.Value, out var placement))
            {
                var location = GetCartesianPoint(placement);
                if (location != null)
                {
                    if (element is IFCBeam beam)
                    {
                        beam.StartPoint = location;
                        // Buscar endpoint através da representação
                        ExtractBeamEndpoints(entity, beam);
                    }
                    else if (element is IFCColumn column)
                    {
                        column.Location = location;
                    }
                    else if (element is IFCFoundation foundation)
                    {
                        foundation.Location = location;
                    }
                }

                // Extrair rotação
                ExtractRotation(placement, element);
            }
        }

        /// <summary>
        /// Extrai pontos finais de uma viga
        /// </summary>
        private void ExtractBeamEndpoints(IFCEntity beamEntity, IFCBeam beam)
        {
            var representationRef = GetReference(beamEntity, 6);
            if (!representationRef.HasValue) return;

            if (_entities.TryGetValue(representationRef.Value, out var representation))
            {
                // Navegar pela estrutura de representação para encontrar a polilinha ou segmento
                var representations = GetReferences(representation, 3);
                foreach (var repRef in representations)
                {
                    if (_entities.TryGetValue(repRef, out var rep))
                    {
                        var items = GetReferences(rep, 3);
                        foreach (var itemRef in items)
                        {
                            if (_entities.TryGetValue(itemRef, out var item))
                            {
                                if (item.Type.ToUpper() == "IFCPOLYLINE")
                                {
                                    var points = GetPolylinePoints(item);
                                    if (points.Count >= 2)
                                    {
                                        beam.StartPoint = points[0];
                                        beam.EndPoint = points[1];
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Extrai perfil do elemento
        /// </summary>
        private void ExtractProfile(IFCEntity entity, IFCElement element)
        {
            // Buscar tipo do elemento
            var typeRefs = GetInverseRelations(entity, "IFCRELDEFINESBYTYPE");
            foreach (var typeRef in typeRefs)
            {
                if (_entities.TryGetValue(typeRef, out var typeRel))
                {
                    var typeEntityRef = GetReference(typeRel, 4);
                    if (typeEntityRef.HasValue && _entities.TryGetValue(typeEntityRef.Value, out var typeEntity))
                    {
                        // Extrair nome do tipo
                        element.TypeName = GetString(typeEntity, 2);

                        // Buscar perfil através das propriedades
                        ExtractProfileFromProperties(typeEntity, element);
                    }
                }
            }
        }

        /// <summary>
        /// Extrai perfil das propriedades do tipo
        /// </summary>
        private void ExtractProfileFromProperties(IFCEntity typeEntity, IFCElement element)
        {
            var propertyRefs = GetInverseRelations(typeEntity, "IFCRELDEFINESBYPROPERTIES");
            foreach (var propRef in propertyRefs)
            {
                if (_entities.TryGetValue(propRef, out var propRel))
                {
                    var propSetRef = GetReference(propRel, 5);
                    if (propSetRef.HasValue && _entities.TryGetValue(propSetRef.Value, out var propSet))
                    {
                        var properties = GetPropertyValues(propSet);
                        
                        // Extrair dimensões do perfil
                        if (element is IFCBeam beam)
                        {
                            beam.Profile = new BeamProfile
                            {
                                Type = properties.GetValueOrDefault("Profile", "Rectangular"),
                                Width = Convert.ToDouble(properties.GetValueOrDefault("Width", "300")),
                                Height = Convert.ToDouble(properties.GetValueOrDefault("Height", "500")),
                                WebThickness = Convert.ToDouble(properties.GetValueOrDefault("WebThickness", "0")),
                                FlangeThickness = Convert.ToDouble(properties.GetValueOrDefault("FlangeThickness", "0"))
                            };
                        }
                        else if (element is IFCColumn column)
                        {
                            column.Profile = new ColumnProfile
                            {
                                Type = properties.GetValueOrDefault("Profile", "Rectangular"),
                                Width = Convert.ToDouble(properties.GetValueOrDefault("Width", "400")),
                                Height = Convert.ToDouble(properties.GetValueOrDefault("Height", "400")),
                                Diameter = Convert.ToDouble(properties.GetValueOrDefault("Diameter", "0"))
                            };
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Extrai material do elemento
        /// </summary>
        private void ExtractMaterial(IFCEntity entity, IFCElement element)
        {
            var materialRefs = GetInverseRelations(entity, "IFCRELASSOCIATESMATERIAL");
            foreach (var matRef in materialRefs)
            {
                if (_entities.TryGetValue(matRef, out var matRel))
                {
                    var materialRef = GetReference(matRel, 5);
                    if (materialRef.HasValue && _entities.TryGetValue(materialRef.Value, out var material))
                    {
                        element.Material = GetString(material, 0);
                        
                        // Tentar obter propriedades do material
                        ExtractMaterialProperties(material, element);
                    }
                }
            }

            // Se não encontrou material, usar padrão
            if (string.IsNullOrEmpty(element.Material))
            {
                element.Material = "Concreto C25";
            }
        }

        /// <summary>
        /// Extrai propriedades do material
        /// </summary>
        private void ExtractMaterialProperties(IFCEntity materialEntity, IFCElement element)
        {
            if (materialEntity.Type.ToUpper() == "IFCMATERIAL")
            {
                element.Material = GetString(materialEntity, 0);
            }
            else if (materialEntity.Type.ToUpper() == "IFCMATERIALLAYERSETUSAGE")
            {
                var layerSetRef = GetReference(materialEntity, 0);
                if (layerSetRef.HasValue && _entities.TryGetValue(layerSetRef.Value, out var layerSet))
                {
                    var layers = GetReferences(layerSet, 0);
                    if (layers.Count > 0 && _entities.TryGetValue(layers[0], out var layer))
                    {
                        var matRef = GetReference(layer, 0);
                        if (matRef.HasValue && _entities.TryGetValue(matRef.Value, out var mat))
                        {
                            element.Material = GetString(mat, 0);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Extrai nível do elemento
        /// </summary>
        private void ExtractLevel(IFCEntity entity, IFCElement element)
        {
            var spatialRefs = GetInverseRelations(entity, "IFCRELCONTAINEDINSPATIALSTRUCTURE");
            foreach (var spatialRef in spatialRefs)
            {
                if (_entities.TryGetValue(spatialRef, out var spatialRel))
                {
                    var storyRef = GetReference(spatialRel, 4);
                    if (storyRef.HasValue && _entities.TryGetValue(storyRef.Value, out var story))
                    {
                        if (story.Type.ToUpper() == "IFCBUILDINGSTOREY")
                        {
                            element.Level = GetString(story, 2);
                            
                            // Obter elevação do nível
                            var elevation = GetElevation(story);
                            if (element is IFCBeam beam)
                            {
                                beam.Elevation = elevation;
                            }
                            else if (element is IFCSlab slab)
                            {
                                slab.Elevation = elevation;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Extrai níveis base e topo de um pilar
        /// </summary>
        private void ExtractColumnLevels(IFCEntity columnEntity, IFCColumn column)
        {
            // Primeiro tentar obter do nível espacial (base)
            ExtractLevel(columnEntity, column);
            column.BaseLevel = column.Level;

            // Buscar propriedades para determinar o topo
            var propertyRefs = GetInverseRelations(columnEntity, "IFCRELDEFINESBYPROPERTIES");
            foreach (var propRef in propertyRefs)
            {
                if (_entities.TryGetValue(propRef, out var propRel))
                {
                    var propSetRef = GetReference(propRel, 5);
                    if (propSetRef.HasValue && _entities.TryGetValue(propSetRef.Value, out var propSet))
                    {
                        var properties = GetPropertyValues(propSet);
                        
                        column.TopLevel = properties.GetValueOrDefault("TopLevel", column.BaseLevel);
                        column.BaseOffset = Convert.ToDouble(properties.GetValueOrDefault("BaseOffset", "0"));
                        column.TopOffset = Convert.ToDouble(properties.GetValueOrDefault("TopOffset", "0"));
                        
                        // Se não tem TopLevel explícito, calcular pela altura
                        if (column.TopLevel == column.BaseLevel)
                        {
                            var height = Convert.ToDouble(properties.GetValueOrDefault("Height", "3000"));
                            // Assumir que vai até o próximo nível
                            column.TopOffset = height;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Extrai geometria da laje
        /// </summary>
        private void ExtractSlabGeometry(IFCEntity slabEntity, IFCSlab slab)
        {
            // Obter espessura
            var propertyRefs = GetInverseRelations(slabEntity, "IFCRELDEFINESBYPROPERTIES");
            foreach (var propRef in propertyRefs)
            {
                if (_entities.TryGetValue(propRef, out var propRel))
                {
                    var propSetRef = GetReference(propRel, 5);
                    if (propSetRef.HasValue && _entities.TryGetValue(propSetRef.Value, out var propSet))
                    {
                        var properties = GetPropertyValues(propSet);
                        slab.Thickness = Convert.ToDouble(properties.GetValueOrDefault("Thickness", "150"));
                        slab.Offset = Convert.ToDouble(properties.GetValueOrDefault("Offset", "0"));
                    }
                }
            }

            // Obter contorno
            ExtractSlabBoundary(slabEntity, slab);
        }

        /// <summary>
        /// Extrai contorno da laje
        /// </summary>
        private void ExtractSlabBoundary(IFCEntity slabEntity, IFCSlab slab)
        {
            var representationRef = GetReference(slabEntity, 6);
            if (!representationRef.HasValue) return;

            if (_entities.TryGetValue(representationRef.Value, out var representation))
            {
                var representations = GetReferences(representation, 3);
                foreach (var repRef in representations)
                {
                    if (_entities.TryGetValue(repRef, out var rep))
                    {
                        var items = GetReferences(rep, 3);
                        foreach (var itemRef in items)
                        {
                            if (_entities.TryGetValue(itemRef, out var item))
                            {
                                if (item.Type.ToUpper() == "IFCFACETEDBREP" || 
                                    item.Type.ToUpper() == "IFCEXTRUDEDAREASOLID")
                                {
                                    // Extrair pontos do contorno
                                    slab.BoundaryPoints = ExtractBoundaryPoints(item);
                                }
                            }
                        }
                    }
                }
            }

            // Se não encontrou contorno, criar retângulo padrão
            if (slab.BoundaryPoints.Count == 0)
            {
                slab.BoundaryPoints.Add(new IFCPoint { X = 0, Y = 0, Z = 0 });
                slab.BoundaryPoints.Add(new IFCPoint { X = 5000, Y = 0, Z = 0 });
                slab.BoundaryPoints.Add(new IFCPoint { X = 5000, Y = 5000, Z = 0 });
                slab.BoundaryPoints.Add(new IFCPoint { X = 0, Y = 5000, Z = 0 });
            }
        }

        /// <summary>
        /// Extrai geometria da fundação
        /// </summary>
        private void ExtractFoundationGeometry(IFCEntity foundationEntity, IFCFoundation foundation)
        {
            // Determinar tipo de fundação
            var predefinedType = GetString(foundationEntity, 8);
            switch (predefinedType?.ToUpper())
            {
                case "FOOTING_BEAM":
                case "STRIP_FOOTING":
                    foundation.FoundationType = FoundationType.Strip;
                    break;
                case "PAD_FOOTING":
                    foundation.FoundationType = FoundationType.Isolated;
                    break;
                case "PILE_CAP":
                    foundation.FoundationType = FoundationType.Isolated;
                    break;
                default:
                    foundation.FoundationType = FoundationType.Isolated;
                    break;
            }

            // Obter propriedades
            var propertyRefs = GetInverseRelations(foundationEntity, "IFCRELDEFINESBYPROPERTIES");
            foreach (var propRef in propertyRefs)
            {
                if (_entities.TryGetValue(propRef, out var propRel))
                {
                    var propSetRef = GetReference(propRel, 5);
                    if (propSetRef.HasValue && _entities.TryGetValue(propSetRef.Value, out var propSet))
                    {
                        var properties = GetPropertyValues(propSet);
                        
                        foundation.Width = Convert.ToDouble(properties.GetValueOrDefault("Width", "1000"));
                        foundation.Height = Convert.ToDouble(properties.GetValueOrDefault("Height", "500"));
                        foundation.Dimensions = new IFCPoint
                        {
                            X = foundation.Width,
                            Y = Convert.ToDouble(properties.GetValueOrDefault("Length", "1000")),
                            Z = foundation.Height
                        };
                    }
                }
            }
        }

        /// <summary>
        /// Obtém ponto cartesiano
        /// </summary>
        private IFCPoint GetCartesianPoint(IFCEntity placementEntity)
        {
            if (placementEntity.Type.ToUpper() == "IFCLOCALPLACEMENT")
            {
                var relativePlacementRef = GetReference(placementEntity, 1);
                if (relativePlacementRef.HasValue && _entities.TryGetValue(relativePlacementRef.Value, out var relativePlacement))
                {
                    if (relativePlacement.Type.ToUpper() == "IFCAXIS2PLACEMENT3D")
                    {
                        var locationRef = GetReference(relativePlacement, 0);
                        if (locationRef.HasValue && _entities.TryGetValue(locationRef.Value, out var location))
                        {
                            return ExtractPoint(location);
                        }
                    }
                }
            }

            return new IFCPoint { X = 0, Y = 0, Z = 0 };
        }

        /// <summary>
        /// Extrai coordenadas de um ponto
        /// </summary>
        private IFCPoint ExtractPoint(IFCEntity pointEntity)
        {
            if (pointEntity.Type.ToUpper() == "IFCCARTESIANPOINT")
            {
                var coords = GetCoordinates(pointEntity, 0);
                if (coords.Count >= 3)
                {
                    return new IFCPoint
                    {
                        X = coords[0],
                        Y = coords[1],
                        Z = coords[2]
                    };
                }
            }

            return new IFCPoint { X = 0, Y = 0, Z = 0 };
        }

        /// <summary>
        /// Extrai rotação do elemento
        /// </summary>
        private void ExtractRotation(IFCEntity placementEntity, IFCElement element)
        {
            // Implementação simplificada - extrair ângulo Z
            element.Rotation = 0; // Por enquanto, sem rotação
        }

        /// <summary>
        /// Obtém pontos de uma polilinha
        /// </summary>
        private List<IFCPoint> GetPolylinePoints(IFCEntity polylineEntity)
        {
            var points = new List<IFCPoint>();
            var pointRefs = GetReferences(polylineEntity, 0);
            
            foreach (var pointRef in pointRefs)
            {
                if (_entities.TryGetValue(pointRef, out var pointEntity))
                {
                    points.Add(ExtractPoint(pointEntity));
                }
            }

            return points;
        }

        /// <summary>
        /// Extrai pontos do contorno
        /// </summary>
        private List<IFCPoint> ExtractBoundaryPoints(IFCEntity solidEntity)
        {
            var points = new List<IFCPoint>();
            
            // Implementação simplificada - retornar lista vazia
            // Em uma implementação completa, navegaríamos pela estrutura B-Rep
            
            return points;
        }

        /// <summary>
        /// Obtém elevação de um nível
        /// </summary>
        private double GetElevation(IFCEntity storyEntity)
        {
            var elevation = GetDouble(storyEntity, 9);
            return elevation ?? 0;
        }

        /// <summary>
        /// Obtém valores de propriedades
        /// </summary>
        private Dictionary<string, string> GetPropertyValues(IFCEntity propertySetEntity)
        {
            var properties = new Dictionary<string, string>();
            
            if (propertySetEntity.Type.ToUpper() == "IFCELEMENTQUANTITY" ||
                propertySetEntity.Type.ToUpper() == "IFCPROPERTYSET")
            {
                var propRefs = GetReferences(propertySetEntity, propertySetEntity.Type.ToUpper() == "IFCPROPERTYSET" ? 4 : 3);
                
                foreach (var propRef in propRefs)
                {
                    if (_entities.TryGetValue(propRef, out var prop))
                    {
                        var name = GetString(prop, 0);
                        var value = GetPropertyValue(prop);
                        
                        if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(value))
                        {
                            properties[name] = value;
                        }
                    }
                }
            }

            return properties;
        }

        /// <summary>
        /// Obtém valor de uma propriedade
        /// </summary>
        private string GetPropertyValue(IFCEntity propertyEntity)
        {
            switch (propertyEntity.Type.ToUpper())
            {
                case "IFCPROPERTYSINGLEVALUE":
                    var nominalValue = propertyEntity.Attributes.ElementAtOrDefault(2);
                    return nominalValue?.ToString() ?? "";
                    
                case "IFCQUANTITYLENGTH":
                case "IFCQUANTITYAREA":
                case "IFCQUANTITYVOLUME":
                    var value = GetDouble(propertyEntity, 3);
                    return value?.ToString() ?? "";
                    
                default:
                    return "";
            }
        }

        /// <summary>
        /// Extrai níveis necessários do modelo
        /// </summary>
        private void ExtractRequiredLevels(IFCModel model)
        {
            var levels = new HashSet<string>();
            
            foreach (var entity in _entities.Values)
            {
                if (entity.Type.ToUpper() == "IFCBUILDINGSTOREY")
                {
                    var name = GetString(entity, 2);
                    var elevation = GetDouble(entity, 9) ?? 0;
                    
                    model.RequiredLevels.Add(new LevelInfo
                    {
                        Name = name,
                        Elevation = elevation
                    });
                }
            }

            // Adicionar níveis dos elementos
            foreach (var element in model.Elements)
            {
                if (!string.IsNullOrEmpty(element.Level))
                    levels.Add(element.Level);
            }
        }

        /// <summary>
        /// Extrai materiais necessários do modelo
        /// </summary>
        private void ExtractRequiredMaterials(IFCModel model)
        {
            var materials = new HashSet<string>();
            
            foreach (var element in model.Elements)
            {
                if (!string.IsNullOrEmpty(element.Material))
                    materials.Add(element.Material);
            }

            model.RequiredMaterials.AddRange(materials);
        }

        /// <summary>
        /// Extrai informações do projeto
        /// </summary>
        private void ExtractProjectInfo(IFCModel model)
        {
            foreach (var entity in _entities.Values)
            {
                if (entity.Type.ToUpper() == "IFCPROJECT")
                {
                    model.ProjectName = GetString(entity, 2);
                    model.ProjectDescription = GetString(entity, 3);
                    break;
                }
            }
        }

        /// <summary>
        /// Obtém relações inversas
        /// </summary>
        private List<int> GetInverseRelations(IFCEntity entity, string relationType)
        {
            var relations = new List<int>();
            
            foreach (var kvp in _entities)
            {
                if (kvp.Value.Type.ToUpper() == relationType.ToUpper())
                {
                    // Verificar se esta relação referencia a entidade
                    var relatedObjects = GetReferences(kvp.Value, 4);
                    if (relatedObjects.Contains(entity.Id))
                    {
                        relations.Add(kvp.Key);
                    }
                }
            }

            return relations;
        }

        // Métodos auxiliares para obter valores dos atributos

        private string GetString(IFCEntity entity, int index)
        {
            if (index < entity.Attributes.Count && entity.Attributes[index] is string str)
                return str;
            return null;
        }

        private double? GetDouble(IFCEntity entity, int index)
        {
            if (index < entity.Attributes.Count)
            {
                if (entity.Attributes[index] is double d)
                    return d;
                if (double.TryParse(entity.Attributes[index]?.ToString(), out double parsed))
                    return parsed;
            }
            return null;
        }

        private int? GetReference(IFCEntity entity, int index)
        {
            if (index < entity.Attributes.Count && entity.Attributes[index] is int refId)
                return refId;
            return null;
        }

        private List<int> GetReferences(IFCEntity entity, int index)
        {
            var refs = new List<int>();
            
            if (index < entity.Attributes.Count && entity.Attributes[index] is List<object> list)
            {
                foreach (var item in list)
                {
                    if (item is int refId)
                        refs.Add(refId);
                }
            }

            return refs;
        }

        private List<double> GetCoordinates(IFCEntity entity, int index)
        {
            var coords = new List<double>();
            
            if (index < entity.Attributes.Count && entity.Attributes[index] is List<object> list)
            {
                foreach (var item in list)
                {
                    if (item is double d)
                        coords.Add(d);
                    else if (double.TryParse(item?.ToString(), out double parsed))
                        coords.Add(parsed);
                }
            }

            return coords;
        }
    }

    /// <summary>
    /// Representa uma entidade IFC
    /// </summary>
    internal class IFCEntity
    {
        public int Id { get; set; }
        public string Type { get; set; }
        public List<object> Attributes { get; set; } = new List<object>();
    }
}
