using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using StrapRevit.Core.IFC;

namespace StrapRevit.Core.Mapping
{
    /// <summary>
    /// Mapeador de elementos IFC para elementos nativos do Revit
    /// </summary>
    public class ElementMapper
    {
        private readonly Document _doc;
        private readonly Dictionary<string, FamilySymbol> _beamTypes;
        private readonly Dictionary<string, FamilySymbol> _columnTypes;
        private readonly Dictionary<string, FloorType> _floorTypes;
        private readonly Dictionary<string, FamilySymbol> _foundationTypes;
        private readonly Dictionary<string, Level> _levels;
        private readonly Dictionary<string, Material> _materials;
        private readonly FamilyLoader _familyLoader;

        public ElementMapper(Document doc)
        {
            _doc = doc;
            _beamTypes = new Dictionary<string, FamilySymbol>();
            _columnTypes = new Dictionary<string, FamilySymbol>();
            _floorTypes = new Dictionary<string, FloorType>();
            _foundationTypes = new Dictionary<string, FamilySymbol>();
            _levels = new Dictionary<string, Level>();
            _materials = new Dictionary<string, Material>();
            _familyLoader = new FamilyLoader(doc);
            
            LoadExistingTypes();
            LoadExistingLevels();
            LoadExistingMaterials();
        }

        /// <summary>
        /// Carrega tipos existentes no projeto
        /// </summary>
        private void LoadExistingTypes()
        {
            // Carregar tipos de viga
            var beamCollector = new FilteredElementCollector(_doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_StructuralFraming)
                .Cast<FamilySymbol>();

            foreach (var type in beamCollector)
            {
                _beamTypes[type.Name] = type;
            }

            // Carregar tipos de pilar
            var columnCollector = new FilteredElementCollector(_doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_StructuralColumns)
                .Cast<FamilySymbol>();

            foreach (var type in columnCollector)
            {
                _columnTypes[type.Name] = type;
            }

            // Carregar tipos de laje
            var floorCollector = new FilteredElementCollector(_doc)
                .OfClass(typeof(FloorType))
                .Cast<FloorType>();

            foreach (var type in floorCollector)
            {
                _floorTypes[type.Name] = type;
            }

            // Carregar tipos de fundação
            var foundationCollector = new FilteredElementCollector(_doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_StructuralFoundation)
                .Cast<FamilySymbol>();

            foreach (var type in foundationCollector)
            {
                _foundationTypes[type.Name] = type;
            }
        }

        /// <summary>
        /// Carrega níveis existentes no projeto
        /// </summary>
        private void LoadExistingLevels()
        {
            var levelCollector = new FilteredElementCollector(_doc)
                .OfClass(typeof(Level))
                .Cast<Level>();

            foreach (var level in levelCollector)
            {
                _levels[level.Name] = level;
            }
        }

        /// <summary>
        /// Carrega materiais existentes no projeto
        /// </summary>
        private void LoadExistingMaterials()
        {
            var materialCollector = new FilteredElementCollector(_doc)
                .OfClass(typeof(Material))
                .Cast<Material>();

            foreach (var material in materialCollector)
            {
                _materials[material.Name] = material;
            }
        }

        /// <summary>
        /// Obtém tipo de viga apropriado
        /// </summary>
        public FamilySymbol GetBeamType(BeamProfile profile, string materialName)
        {
            // Gerar nome do tipo baseado no perfil
            var typeName = GenerateBeamTypeName(profile, materialName);
            
            // Verificar se já existe
            if (_beamTypes.TryGetValue(typeName, out var existingType))
            {
                if (!existingType.IsActive)
                    existingType.Activate();
                return existingType;
            }

            // Buscar tipo base apropriado
            FamilySymbol baseType = null;
            string intendedFamilyName = "";
            
            switch (profile.Type?.ToUpper())
            {
                case "I":
                case "IPE":
                case "HEA":
                case "HEB":
                    intendedFamilyName = "Viga-I";
                    baseType = FindOrLoadBeamFamily("Viga-I", "I-Wide Flange");
                    break;
                    
                case "RECTANGULAR":
                case "RETANGULAR":
                    intendedFamilyName = "Viga-Retangular";
                    baseType = FindOrLoadBeamFamily("Viga-Retangular", "Concrete-Rectangular Beam");
                    break;
                    
                case "T":
                    intendedFamilyName = "Viga-T";
                    baseType = FindOrLoadBeamFamily("Viga-T", "T-Beam");
                    break;
                    
                case "L":
                    intendedFamilyName = "Viga-L";
                    baseType = FindOrLoadBeamFamily("Viga-L", "L-Beam");
                    break;
                    
                case "CIRCULAR":
                    intendedFamilyName = "Viga-Circular";
                    baseType = FindOrLoadBeamFamily("Viga-Circular", "Concrete-Round Beam");
                    break;
                    
                default:
                    intendedFamilyName = "Viga-Retangular";
                    baseType = FindOrLoadBeamFamily("Viga-Retangular", "Concrete-Rectangular Beam");
                    break;
            }

            // Se não encontrou o tipo específico, usar fallback genérico
            if (baseType == null)
            {
                baseType = GetFallbackBeamType(out string fallbackName);
                if (baseType != null)
                {
                    // Criar tipo com nome indicando o fallback
                    var fallbackTypeName = $"{typeName} [FALLBACK: {intendedFamilyName}]";
                    var newType = baseType.Duplicate(fallbackTypeName) as FamilySymbol;
                    
                    // Tentar ajustar parâmetros se possível
                    TrySetBeamTypeParameters(newType, profile);
                    
                    if (!newType.IsActive)
                        newType.Activate();
                    
                    _beamTypes[typeName] = newType;
                    return newType;
                }
                return null;
            }

            // Criar novo tipo com dimensões específicas
            var regularType = baseType.Duplicate(typeName) as FamilySymbol;
            
            // Definir parâmetros do tipo
            SetBeamTypeParameters(regularType, profile);
            
            // Ativar tipo
            if (!regularType.IsActive)
                regularType.Activate();
            
            _beamTypes[typeName] = regularType;
            return regularType;
        }

        /// <summary>
        /// Obtém tipo de pilar apropriado
        /// </summary>
        public FamilySymbol GetColumnType(ColumnProfile profile, string materialName)
        {
            var typeName = GenerateColumnTypeName(profile, materialName);
            
            if (_columnTypes.TryGetValue(typeName, out var existingType))
            {
                if (!existingType.IsActive)
                    existingType.Activate();
                return existingType;
            }

            FamilySymbol baseType = null;
            string intendedFamilyName = "";
            
            switch (profile.Type?.ToUpper())
            {
                case "CIRCULAR":
                    intendedFamilyName = "Pilar-Circular";
                    baseType = FindOrLoadColumnFamily("Pilar-Circular", "Concrete-Round-Column");
                    break;
                    
                case "RECTANGULAR":
                case "RETANGULAR":
                default:
                    intendedFamilyName = "Pilar-Retangular";
                    baseType = FindOrLoadColumnFamily("Pilar-Retangular", "Concrete-Rectangular-Column");
                    break;
            }

            // Se não encontrou o tipo específico, usar fallback genérico
            if (baseType == null)
            {
                baseType = GetFallbackColumnType(out string fallbackName);
                if (baseType != null)
                {
                    // Criar tipo com nome indicando o fallback
                    var fallbackTypeName = $"{typeName} [FALLBACK: {intendedFamilyName}]";
                    var newType = baseType.Duplicate(fallbackTypeName) as FamilySymbol;
                    
                    // Tentar ajustar parâmetros se possível
                    TrySetColumnTypeParameters(newType, profile);
                    
                    if (!newType.IsActive)
                        newType.Activate();
                    
                    _columnTypes[typeName] = newType;
                    return newType;
                }
                return null;
            }

            var regularType = baseType.Duplicate(typeName) as FamilySymbol;
            SetColumnTypeParameters(regularType, profile);
            
            if (!regularType.IsActive)
                regularType.Activate();
            
            _columnTypes[typeName] = regularType;
            return regularType;
        }

        /// <summary>
        /// Obtém tipo de laje apropriado
        /// </summary>
        public FloorType GetFloorType(double thickness, string materialName)
        {
            var typeName = $"Laje {thickness:F0}mm - {materialName}";
            
            if (_floorTypes.TryGetValue(typeName, out var existingType))
                return existingType;

            // Buscar tipo base genérico
            var baseType = _floorTypes.Values.FirstOrDefault(t => t.Name.Contains("Concreto") || t.Name.Contains("Concrete"));
            
            if (baseType == null)
                baseType = _floorTypes.Values.FirstOrDefault();
                
            if (baseType == null)
                return null;

            // Duplicar e ajustar espessura
            var newType = baseType.Duplicate(typeName) as FloorType;
            
            // Ajustar espessura
            var compoundStructure = newType.GetCompoundStructure();
            if (compoundStructure != null)
            {
                var layers = compoundStructure.GetLayers();
                if (layers.Count > 0)
                {
                    // Converter mm para pés
                    var thicknessInFeet = thickness / 304.8;
                    
                    // Ajustar camada principal
                    var mainLayer = layers[0];
                    layers[0] = new CompoundStructureLayer(
                        thicknessInFeet,
                        mainLayer.Function,
                        mainLayer.MaterialId
                    );
                    
                    compoundStructure.SetLayers(layers);
                    newType.SetCompoundStructure(compoundStructure);
                }
            }
            
            _floorTypes[typeName] = newType;
            return newType;
        }

        /// <summary>
        /// Obtém tipo de fundação isolada
        /// </summary>
        public FamilySymbol GetIsolatedFoundationType(IFCPoint dimensions, string materialName)
        {
            var typeName = $"Sapata {dimensions.X:F0}x{dimensions.Y:F0}x{dimensions.Z:F0} - {materialName}";
            
            if (_foundationTypes.TryGetValue(typeName, out var existingType))
            {
                if (!existingType.IsActive)
                    existingType.Activate();
                return existingType;
            }

            var baseType = FindOrLoadFoundationFamily("Sapata-Isolada", "Footing-Rectangular");
            if (baseType == null)
                return null;

            var newType = baseType.Duplicate(typeName) as FamilySymbol;
            
            // Definir dimensões
            newType.get_Parameter(BuiltInParameter.STRUCTURAL_FOUNDATION_WIDTH)?.Set(dimensions.X / 304.8);
            newType.get_Parameter(BuiltInParameter.STRUCTURAL_FOUNDATION_LENGTH)?.Set(dimensions.Y / 304.8);
            newType.get_Parameter(BuiltInParameter.STRUCTURAL_FOUNDATION_THICKNESS)?.Set(dimensions.Z / 304.8);
            
            if (!newType.IsActive)
                newType.Activate();
            
            _foundationTypes[typeName] = newType;
            return newType;
        }

        /// <summary>
        /// Obtém tipo de fundação corrida
        /// </summary>
        public FamilySymbol GetStripFoundationType(double width, double height, string materialName)
        {
            var typeName = $"Sapata Corrida {width:F0}x{height:F0} - {materialName}";
            
            if (_foundationTypes.TryGetValue(typeName, out var existingType))
            {
                if (!existingType.IsActive)
                    existingType.Activate();
                return existingType;
            }

            var baseType = FindOrLoadFoundationFamily("Sapata-Corrida", "Wall Footing");
            if (baseType == null)
                return null;

            var newType = baseType.Duplicate(typeName) as FamilySymbol;
            
            // Definir dimensões
            newType.get_Parameter(BuiltInParameter.STRUCTURAL_FOUNDATION_WIDTH)?.Set(width / 304.8);
            newType.get_Parameter(BuiltInParameter.STRUCTURAL_FOUNDATION_THICKNESS)?.Set(height / 304.8);
            
            if (!newType.IsActive)
                newType.Activate();
            
            _foundationTypes[typeName] = newType;
            return newType;
        }

        /// <summary>
        /// Obtém tipo de laje estrutural para fundação
        /// </summary>
        public FloorType GetStructuralFloorType(double thickness, string materialName)
        {
            var typeName = $"Radier {thickness:F0}mm - {materialName}";
            
            if (_floorTypes.TryGetValue(typeName, out var existingType))
                return existingType;

            // Buscar tipo estrutural base
            var baseType = _floorTypes.Values.FirstOrDefault(t => 
                t.get_Parameter(BuiltInParameter.FLOOR_PARAM_IS_STRUCTURAL)?.AsInteger() == 1);
            
            if (baseType == null)
                baseType = GetFloorType(thickness, materialName);
                
            if (baseType != null)
            {
                // Marcar como estrutural
                baseType.get_Parameter(BuiltInParameter.FLOOR_PARAM_IS_STRUCTURAL)?.Set(1);
            }
            
            return baseType;
        }

        /// <summary>
        /// Obtém nível por nome
        /// </summary>
        public Level GetLevel(string levelName)
        {
            if (string.IsNullOrEmpty(levelName))
                return null;
                
            // Tentar correspondência exata
            if (_levels.TryGetValue(levelName, out var level))
                return level;
            
            // Tentar correspondência parcial
            foreach (var kvp in _levels)
            {
                if (kvp.Key.Contains(levelName) || levelName.Contains(kvp.Key))
                    return kvp.Value;
            }
            
            // Tentar por elevação se o nome contém número
            if (double.TryParse(System.Text.RegularExpressions.Regex.Match(levelName, @"[-+]?\d*\.?\d+").Value, out double elevation))
            {
                // Converter para pés se necessário
                var elevationInFeet = elevation > 100 ? elevation / 304.8 : elevation;
                
                // Buscar nível mais próximo
                return _levels.Values.OrderBy(l => Math.Abs(l.Elevation - elevationInFeet)).FirstOrDefault();
            }
            
            return null;
        }

        /// <summary>
        /// Obtém material por nome
        /// </summary>
        public Material GetMaterial(string materialName)
        {
            if (string.IsNullOrEmpty(materialName))
                return null;
                
            if (_materials.TryGetValue(materialName, out var material))
                return material;
            
            // Tentar correspondência parcial
            foreach (var kvp in _materials)
            {
                if (kvp.Key.Contains(materialName) || materialName.Contains(kvp.Key))
                    return kvp.Value;
            }
            
            // Material padrão de concreto
            return _materials.Values.FirstOrDefault(m => 
                m.Name.Contains("Concreto") || m.Name.Contains("Concrete"));
        }

        /// <summary>
        /// Atualiza mapeamento de níveis
        /// </summary>
        public void UpdateLevelMapping(Dictionary<string, Level> newLevels)
        {
            foreach (var kvp in newLevels)
            {
                _levels[kvp.Key] = kvp.Value;
            }
        }

        /// <summary>
        /// Atualiza mapeamento de materiais
        /// </summary>
        public void UpdateMaterialMapping(Dictionary<string, Material> newMaterials)
        {
            foreach (var kvp in newMaterials)
            {
                _materials[kvp.Key] = kvp.Value;
            }
        }

        /// <summary>
        /// Gera nome do tipo de viga
        /// </summary>
        private string GenerateBeamTypeName(BeamProfile profile, string materialName)
        {
            var prefix = profile.Type ?? "Retangular";
            
            if (profile.Type?.ToUpper() == "RECTANGULAR" || profile.Type?.ToUpper() == "RETANGULAR")
            {
                return $"V-{prefix} {profile.Width:F0}x{profile.Height:F0} - {materialName}";
            }
            else if (profile.Type?.ToUpper() == "I")
            {
                return $"V-{prefix} H{profile.Height:F0} - {materialName}";
            }
            
            return $"V-{prefix} - {materialName}";
        }

        /// <summary>
        /// Gera nome do tipo de pilar
        /// </summary>
        private string GenerateColumnTypeName(ColumnProfile profile, string materialName)
        {
            if (profile.Type?.ToUpper() == "CIRCULAR")
            {
                return $"P-Circular D{profile.Diameter:F0} - {materialName}";
            }
            else
            {
                return $"P-Retangular {profile.Width:F0}x{profile.Height:F0} - {materialName}";
            }
        }

        /// <summary>
        /// Define parâmetros do tipo de viga
        /// </summary>
        private void SetBeamTypeParameters(FamilySymbol beamType, BeamProfile profile)
        {
            // Converter mm para pés
            const double mmToFeet = 1.0 / 304.8;
            
            // Parâmetros comuns
            beamType.get_Parameter(BuiltInParameter.STRUCTURAL_SECTION_SHAPE)?.Set(1); // Retangular
            
            // Dimensões principais
            var widthParam = beamType.LookupParameter("b") ?? beamType.LookupParameter("Width");
            widthParam?.Set(profile.Width * mmToFeet);
            
            var heightParam = beamType.LookupParameter("h") ?? beamType.LookupParameter("Height");
            heightParam?.Set(profile.Height * mmToFeet);
            
            // Para perfis I
            if (profile.WebThickness > 0)
            {
                var webParam = beamType.LookupParameter("tw") ?? beamType.LookupParameter("Web Thickness");
                webParam?.Set(profile.WebThickness * mmToFeet);
            }
            
            if (profile.FlangeThickness > 0)
            {
                var flangeParam = beamType.LookupParameter("tf") ?? beamType.LookupParameter("Flange Thickness");
                flangeParam?.Set(profile.FlangeThickness * mmToFeet);
            }
        }

        /// <summary>
        /// Define parâmetros do tipo de pilar
        /// </summary>
        private void SetColumnTypeParameters(FamilySymbol columnType, ColumnProfile profile)
        {
            const double mmToFeet = 1.0 / 304.8;
            
            if (profile.Type?.ToUpper() == "CIRCULAR")
            {
                var diameterParam = columnType.LookupParameter("d") ?? columnType.LookupParameter("Diameter");
                diameterParam?.Set(profile.Diameter * mmToFeet);
            }
            else
            {
                var widthParam = columnType.LookupParameter("b") ?? columnType.LookupParameter("Width");
                widthParam?.Set(profile.Width * mmToFeet);
                
                var depthParam = columnType.LookupParameter("h") ?? columnType.LookupParameter("Depth");
                depthParam?.Set(profile.Height * mmToFeet);
            }
        }

        /// <summary>
        /// Busca ou carrega família de viga
        /// </summary>
        private FamilySymbol FindOrLoadBeamFamily(string preferredName, string fallbackName)
        {
            // Buscar tipo existente
            var existingType = _beamTypes.Values.FirstOrDefault(t => 
                t.Family.Name.Contains(preferredName) || t.Family.Name.Contains(fallbackName));
            
            if (existingType != null)
                return existingType;
            
            // Tentar carregar família
            var loadedFamily = _familyLoader.LoadStructuralFramingFamily(preferredName) ?? 
                              _familyLoader.LoadStructuralFramingFamily(fallbackName);
            
            if (loadedFamily != null)
            {
                // Obter primeiro tipo da família
                var typeIds = loadedFamily.GetFamilySymbolIds();
                if (typeIds.Any())
                {
                    var type = _doc.GetElement(typeIds.First()) as FamilySymbol;
                    if (type != null)
                    {
                        _beamTypes[type.Name] = type;
                        return type;
                    }
                }
            }
            
            // Fallback para qualquer tipo disponível
            return _beamTypes.Values.FirstOrDefault();
        }

        /// <summary>
        /// Busca ou carrega família de pilar
        /// </summary>
        private FamilySymbol FindOrLoadColumnFamily(string preferredName, string fallbackName)
        {
            var existingType = _columnTypes.Values.FirstOrDefault(t => 
                t.Family.Name.Contains(preferredName) || t.Family.Name.Contains(fallbackName));
            
            if (existingType != null)
                return existingType;
            
            var loadedFamily = _familyLoader.LoadStructuralColumnFamily(preferredName) ?? 
                              _familyLoader.LoadStructuralColumnFamily(fallbackName);
            
            if (loadedFamily != null)
            {
                var typeIds = loadedFamily.GetFamilySymbolIds();
                if (typeIds.Any())
                {
                    var type = _doc.GetElement(typeIds.First()) as FamilySymbol;
                    if (type != null)
                    {
                        _columnTypes[type.Name] = type;
                        return type;
                    }
                }
            }
            
            return _columnTypes.Values.FirstOrDefault();
        }

        /// <summary>
        /// Busca ou carrega família de fundação
        /// </summary>
        private FamilySymbol FindOrLoadFoundationFamily(string preferredName, string fallbackName)
        {
            var existingType = _foundationTypes.Values.FirstOrDefault(t => 
                t.Family.Name.Contains(preferredName) || t.Family.Name.Contains(fallbackName));
            
            if (existingType != null)
                return existingType;
            
            var loadedFamily = _familyLoader.LoadStructuralFoundationFamily(preferredName) ?? 
                              _familyLoader.LoadStructuralFoundationFamily(fallbackName);
            
            if (loadedFamily != null)
            {
                var typeIds = loadedFamily.GetFamilySymbolIds();
                if (typeIds.Any())
                {
                    var type = _doc.GetElement(typeIds.First()) as FamilySymbol;
                    if (type != null)
                    {
                        _foundationTypes[type.Name] = type;
                        return type;
                    }
                }
            }
            
            return _foundationTypes.Values.FirstOrDefault();
        }

        /// <summary>
        /// Obtém tipo de pilar fallback (qualquer disponível)
        /// </summary>
        private FamilySymbol GetFallbackColumnType(out string fallbackName)
        {
            fallbackName = "";
            
            // Tentar qualquer tipo de pilar disponível no projeto
            var availableColumnType = _columnTypes.Values.FirstOrDefault();
            if (availableColumnType != null)
            {
                fallbackName = availableColumnType.Family.Name;
                return availableColumnType;
            }

            // Se não há tipos carregados, tentar carregar um genérico
            var genericFamily = LoadGenericColumnFamily();
            if (genericFamily != null)
            {
                var typeIds = genericFamily.GetFamilySymbolIds();
                if (typeIds.Any())
                {
                    var type = _doc.GetElement(typeIds.First()) as FamilySymbol;
                    if (type != null)
                    {
                        fallbackName = type.Family.Name;
                        _columnTypes[type.Name] = type;
                        return type;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Obtém tipo de viga fallback (qualquer disponível)
        /// </summary>
        private FamilySymbol GetFallbackBeamType(out string fallbackName)
        {
            fallbackName = "";
            
            // Tentar qualquer tipo de viga disponível no projeto
            var availableBeamType = _beamTypes.Values.FirstOrDefault();
            if (availableBeamType != null)
            {
                fallbackName = availableBeamType.Family.Name;
                return availableBeamType;
            }

            // Se não há tipos carregados, tentar carregar um genérico
            var genericFamily = LoadGenericBeamFamily();
            if (genericFamily != null)
            {
                var typeIds = genericFamily.GetFamilySymbolIds();
                if (typeIds.Any())
                {
                    var type = _doc.GetElement(typeIds.First()) as FamilySymbol;
                    if (type != null)
                    {
                        fallbackName = type.Family.Name;
                        _beamTypes[type.Name] = type;
                        return type;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Carrega família genérica de pilar
        /// </summary>
        private Family LoadGenericColumnFamily()
        {
            // Tentar carregar família mais básica possível
            var basicFamilies = new[]
            {
                "M_Concrete-Rectangular-Column",
                "Concrete-Rectangular-Column", 
                "M_Structural Column",
                "Structural Column"
            };

            foreach (var familyName in basicFamilies)
            {
                var family = _familyLoader.LoadStructuralColumnFamily(familyName);
                if (family != null)
                    return family;
            }

            return null;
        }

        /// <summary>
        /// Carrega família genérica de viga
        /// </summary>
        private Family LoadGenericBeamFamily()
        {
            // Tentar carregar família mais básica possível
            var basicFamilies = new[]
            {
                "M_Concrete-Rectangular Beam",
                "Concrete-Rectangular Beam",
                "M_Structural Framing",
                "Structural Framing"
            };

            foreach (var familyName in basicFamilies)
            {
                var family = _familyLoader.LoadStructuralFramingFamily(familyName);
                if (family != null)
                    return family;
            }

            return null;
        }

        /// <summary>
        /// Tenta definir parâmetros do tipo de pilar (versão segura)
        /// </summary>
        private void TrySetColumnTypeParameters(FamilySymbol columnType, ColumnProfile profile)
        {
            try
            {
                SetColumnTypeParameters(columnType, profile);
            }
            catch
            {
                // Se não conseguir definir parâmetros específicos, ignorar
                // O elemento será criado com dimensões padrão da família
            }
        }

        /// <summary>
        /// Tenta definir parâmetros do tipo de viga (versão segura)
        /// </summary>
        private void TrySetBeamTypeParameters(FamilySymbol beamType, BeamProfile profile)
        {
            try
            {
                SetBeamTypeParameters(beamType, profile);
            }
            catch
            {
                // Se não conseguir definir parâmetros específicos, ignorar
                // O elemento será criado com dimensões padrão da família
            }
        }
    }
}
