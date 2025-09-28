using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace StrapRevit.Core.Mapping
{
    /// <summary>
    /// Criador de materiais do Revit
    /// </summary>
    public class MaterialCreator
    {
        private readonly Document _doc;
        private readonly Dictionary<string, Material> _existingMaterials;

        public MaterialCreator(Document doc)
        {
            _doc = doc;
            _existingMaterials = new Dictionary<string, Material>();
            LoadExistingMaterials();
        }

        /// <summary>
        /// Carrega materiais existentes
        /// </summary>
        private void LoadExistingMaterials()
        {
            var materials = new FilteredElementCollector(_doc)
                .OfClass(typeof(Material))
                .Cast<Material>();

            foreach (var material in materials)
            {
                _existingMaterials[material.Name] = material;
            }
        }

        /// <summary>
        /// Cria materiais necessários
        /// </summary>
        public Dictionary<string, Material> CreateRequiredMaterials(List<string> requiredMaterials)
        {
            var createdMaterials = new Dictionary<string, Material>();

            foreach (var materialName in requiredMaterials.Distinct())
            {
                if (string.IsNullOrEmpty(materialName))
                    continue;

                // Verificar se já existe
                if (_existingMaterials.ContainsKey(materialName))
                {
                    continue;
                }

                // Tentar encontrar material similar
                var similarMaterial = FindSimilarMaterial(materialName);
                if (similarMaterial != null)
                {
                    createdMaterials[materialName] = similarMaterial;
                    continue;
                }

                // Criar novo material
                try
                {
                    var newMaterial = CreateMaterial(materialName);
                    if (newMaterial != null)
                    {
                        createdMaterials[materialName] = newMaterial;
                        _existingMaterials[newMaterial.Name] = newMaterial;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Erro ao criar material {materialName}: {ex.Message}");
                }
            }

            return createdMaterials;
        }

        /// <summary>
        /// Cria novo material
        /// </summary>
        private Material CreateMaterial(string materialName)
        {
            // Determinar tipo de material baseado no nome
            var materialType = DetermineMaterialType(materialName);
            
            // Criar material base
            ElementId materialId = Material.Create(_doc, GenerateUniqueMaterialName(materialName));
            var material = _doc.GetElement(materialId) as Material;

            if (material == null)
                return null;

            // Configurar propriedades básicas
            ConfigureMaterialProperties(material, materialType);

            // Configurar aparência se possível
            ConfigureMaterialAppearance(material, materialType);

            return material;
        }

        /// <summary>
        /// Determina tipo de material baseado no nome
        /// </summary>
        private MaterialType DetermineMaterialType(string materialName)
        {
            var nameLower = materialName.ToLower();

            if (nameLower.Contains("concreto") || nameLower.Contains("concrete") || 
                nameLower.Contains("c25") || nameLower.Contains("c30") || nameLower.Contains("c40"))
            {
                return MaterialType.Concrete;
            }
            else if (nameLower.Contains("aço") || nameLower.Contains("steel") || 
                     nameLower.Contains("ca50") || nameLower.Contains("ca60"))
            {
                return MaterialType.Steel;
            }
            else if (nameLower.Contains("madeira") || nameLower.Contains("wood"))
            {
                return MaterialType.Wood;
            }
            else if (nameLower.Contains("alvenaria") || nameLower.Contains("masonry") || 
                     nameLower.Contains("tijolo") || nameLower.Contains("brick"))
            {
                return MaterialType.Masonry;
            }

            return MaterialType.Generic;
        }

        /// <summary>
        /// Configura propriedades do material
        /// </summary>
        private void ConfigureMaterialProperties(Material material, MaterialType type)
        {
            // Definir cor básica
            switch (type)
            {
                case MaterialType.Concrete:
                    material.Color = new Color(192, 192, 192); // Cinza
                    material.Transparency = 0;
                    break;
                    
                case MaterialType.Steel:
                    material.Color = new Color(128, 128, 128); // Cinza escuro
                    material.Transparency = 0;
                    break;
                    
                case MaterialType.Wood:
                    material.Color = new Color(139, 69, 19); // Marrom
                    material.Transparency = 0;
                    break;
                    
                case MaterialType.Masonry:
                    material.Color = new Color(178, 34, 34); // Vermelho tijolo
                    material.Transparency = 0;
                    break;
                    
                default:
                    material.Color = new Color(200, 200, 200);
                    material.Transparency = 0;
                    break;
            }

            // Definir padrão de superfície
            try
            {
                material.SurfaceForegroundPatternId = GetSurfacePattern(type);
                material.CutForegroundPatternId = GetCutPattern(type);
            }
            catch
            {
                // APIs podem não estar disponíveis em algumas versões
            }
        }

        /// <summary>
        /// Configura aparência do material
        /// </summary>
        private void ConfigureMaterialAppearance(Material material, MaterialType type)
        {
            try
            {
                // Obter asset de aparência existente similar
                ElementId appearanceAssetId = GetAppearanceAsset(type);
                
                if (appearanceAssetId != ElementId.InvalidElementId)
                {
                    material.AppearanceAssetId = appearanceAssetId;
                }
            }
            catch
            {
                // Ignorar erros de aparência
            }
        }

        /// <summary>
        /// Obtém padrão de superfície apropriado
        /// </summary>
        private ElementId GetSurfacePattern(MaterialType type)
        {
            var patterns = new FilteredElementCollector(_doc)
                .OfClass(typeof(FillPatternElement))
                .Cast<FillPatternElement>()
                .Where(p => p.GetFillPattern().Target == FillPatternTarget.Model);

            FillPatternElement pattern = null;

            switch (type)
            {
                case MaterialType.Concrete:
                    pattern = patterns.FirstOrDefault(p => p.Name.Contains("Concrete") || p.Name.Contains("Concreto"));
                    break;
                    
                case MaterialType.Steel:
                    pattern = patterns.FirstOrDefault(p => p.Name.Contains("Steel") || p.Name.Contains("Aço"));
                    break;
                    
                case MaterialType.Wood:
                    pattern = patterns.FirstOrDefault(p => p.Name.Contains("Wood") || p.Name.Contains("Madeira"));
                    break;
                    
                case MaterialType.Masonry:
                    pattern = patterns.FirstOrDefault(p => p.Name.Contains("Brick") || p.Name.Contains("Tijolo"));
                    break;
            }

            return pattern?.Id ?? ElementId.InvalidElementId;
        }

        /// <summary>
        /// Obtém padrão de corte apropriado
        /// </summary>
        private ElementId GetCutPattern(MaterialType type)
        {
            var patterns = new FilteredElementCollector(_doc)
                .OfClass(typeof(FillPatternElement))
                .Cast<FillPatternElement>()
                .Where(p => p.GetFillPattern().Target == FillPatternTarget.Drafting);

            FillPatternElement pattern = null;

            switch (type)
            {
                case MaterialType.Concrete:
                    pattern = patterns.FirstOrDefault(p => p.Name.Contains("Concrete") || p.Name.Contains("Sand"));
                    break;
                    
                case MaterialType.Steel:
                    pattern = patterns.FirstOrDefault(p => p.Name.Contains("Steel") || p.Name.Contains("Solid"));
                    break;
                    
                case MaterialType.Wood:
                    pattern = patterns.FirstOrDefault(p => p.Name.Contains("Wood") || p.Name.Contains("Diagonal"));
                    break;
                    
                case MaterialType.Masonry:
                    pattern = patterns.FirstOrDefault(p => p.Name.Contains("Brick") || p.Name.Contains("Diagonal"));
                    break;
            }

            // Se não encontrar específico, usar sólido
            if (pattern == null)
            {
                pattern = patterns.FirstOrDefault(p => p.Name.Contains("Solid") || p.Name == "Sólido");
            }

            return pattern?.Id ?? ElementId.InvalidElementId;
        }

        /// <summary>
        /// Obtém asset de aparência
        /// </summary>
        private ElementId GetAppearanceAsset(MaterialType type)
        {
            var assets = new FilteredElementCollector(_doc)
                .OfClass(typeof(AppearanceAssetElement))
                .Cast<AppearanceAssetElement>();

            AppearanceAssetElement asset = null;

            switch (type)
            {
                case MaterialType.Concrete:
                    asset = assets.FirstOrDefault(a => 
                        a.Name.Contains("Concrete") || a.Name.Contains("Concreto"));
                    break;
                    
                case MaterialType.Steel:
                    asset = assets.FirstOrDefault(a => 
                        a.Name.Contains("Steel") || a.Name.Contains("Metal"));
                    break;
                    
                case MaterialType.Wood:
                    asset = assets.FirstOrDefault(a => 
                        a.Name.Contains("Wood") || a.Name.Contains("Madeira"));
                    break;
                    
                case MaterialType.Masonry:
                    asset = assets.FirstOrDefault(a => 
                        a.Name.Contains("Brick") || a.Name.Contains("Masonry"));
                    break;
            }

            // Fallback genérico
            if (asset == null)
            {
                asset = assets.FirstOrDefault(a => a.Name.Contains("Generic"));
            }

            return asset?.Id ?? ElementId.InvalidElementId;
        }

        /// <summary>
        /// Encontra material similar existente
        /// </summary>
        private Material FindSimilarMaterial(string materialName)
        {
            // Procurar correspondência parcial
            foreach (var kvp in _existingMaterials)
            {
                if (kvp.Key.Contains(materialName) || materialName.Contains(kvp.Key))
                {
                    return kvp.Value;
                }
            }

            // Procurar por tipo
            var type = DetermineMaterialType(materialName);
            
            switch (type)
            {
                case MaterialType.Concrete:
                    return _existingMaterials.Values.FirstOrDefault(m => 
                        m.Name.Contains("Concreto") || m.Name.Contains("Concrete"));
                        
                case MaterialType.Steel:
                    return _existingMaterials.Values.FirstOrDefault(m => 
                        m.Name.Contains("Aço") || m.Name.Contains("Steel"));
                        
                case MaterialType.Wood:
                    return _existingMaterials.Values.FirstOrDefault(m => 
                        m.Name.Contains("Madeira") || m.Name.Contains("Wood"));
                        
                case MaterialType.Masonry:
                    return _existingMaterials.Values.FirstOrDefault(m => 
                        m.Name.Contains("Alvenaria") || m.Name.Contains("Masonry"));
            }

            return null;
        }

        /// <summary>
        /// Gera nome único para o material
        /// </summary>
        private string GenerateUniqueMaterialName(string baseName)
        {
            if (!_existingMaterials.ContainsKey(baseName))
                return baseName;

            int counter = 1;
            string uniqueName;
            
            do
            {
                uniqueName = $"{baseName} ({counter})";
                counter++;
            } while (_existingMaterials.ContainsKey(uniqueName));

            return uniqueName;
        }

        /// <summary>
        /// Tipo de material
        /// </summary>
        private enum MaterialType
        {
            Generic,
            Concrete,
            Steel,
            Wood,
            Masonry
        }
    }
}
