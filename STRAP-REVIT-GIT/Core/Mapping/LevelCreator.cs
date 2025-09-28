using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using StrapRevit.Core.IFC;

namespace StrapRevit.Core.Mapping
{
    /// <summary>
    /// Criador de níveis do Revit
    /// </summary>
    public class LevelCreator
    {
        private readonly Document _doc;
        private readonly Dictionary<string, Level> _existingLevels;

        public LevelCreator(Document doc)
        {
            _doc = doc;
            _existingLevels = new Dictionary<string, Level>();
            LoadExistingLevels();
        }

        /// <summary>
        /// Carrega níveis existentes
        /// </summary>
        private void LoadExistingLevels()
        {
            var levels = new FilteredElementCollector(_doc)
                .OfClass(typeof(Level))
                .Cast<Level>();

            foreach (var level in levels)
            {
                _existingLevels[level.Name] = level;
            }
        }

        /// <summary>
        /// Cria níveis necessários
        /// </summary>
        public Dictionary<string, Level> CreateRequiredLevels(List<LevelInfo> requiredLevels)
        {
            var createdLevels = new Dictionary<string, Level>();

            foreach (var levelInfo in requiredLevels)
            {
                // Verificar se já existe
                if (_existingLevels.ContainsKey(levelInfo.Name))
                {
                    continue;
                }

                // Verificar se existe nível na mesma elevação
                var elevationInFeet = levelInfo.Elevation / 304.8; // Converter mm para pés
                var existingAtElevation = _existingLevels.Values
                    .FirstOrDefault(l => Math.Abs(l.Elevation - elevationInFeet) < 0.001);

                if (existingAtElevation != null)
                {
                    // Usar nível existente
                    createdLevels[levelInfo.Name] = existingAtElevation;
                    continue;
                }

                // Criar novo nível
                try
                {
                    var newLevel = Level.Create(_doc, elevationInFeet);
                    newLevel.Name = GenerateUniqueLevelName(levelInfo.Name);
                    
                    createdLevels[levelInfo.Name] = newLevel;
                    _existingLevels[newLevel.Name] = newLevel;
                }
                catch (Exception ex)
                {
                    // Log do erro mas continuar
                    System.Diagnostics.Debug.WriteLine($"Erro ao criar nível {levelInfo.Name}: {ex.Message}");
                }
            }

            return createdLevels;
        }

        /// <summary>
        /// Gera nome único para o nível
        /// </summary>
        private string GenerateUniqueLevelName(string baseName)
        {
            if (!_existingLevels.ContainsKey(baseName))
                return baseName;

            int counter = 1;
            string uniqueName;
            
            do
            {
                uniqueName = $"{baseName} ({counter})";
                counter++;
            } while (_existingLevels.ContainsKey(uniqueName));

            return uniqueName;
        }

        /// <summary>
        /// Obtém ou cria nível por elevação
        /// </summary>
        public Level GetOrCreateLevelByElevation(double elevationInMm, string suggestedName = null)
        {
            var elevationInFeet = elevationInMm / 304.8;
            
            // Procurar nível existente próximo (tolerância de 10mm)
            var existingLevel = _existingLevels.Values
                .FirstOrDefault(l => Math.Abs(l.Elevation - elevationInFeet) < 0.033); // ~10mm

            if (existingLevel != null)
                return existingLevel;

            // Criar novo nível
            var newLevel = Level.Create(_doc, elevationInFeet);
            
            if (!string.IsNullOrEmpty(suggestedName))
            {
                newLevel.Name = GenerateUniqueLevelName(suggestedName);
            }
            else
            {
                // Gerar nome baseado na elevação
                var elevationText = elevationInMm >= 0 ? $"+{elevationInMm:F0}" : $"{elevationInMm:F0}";
                newLevel.Name = GenerateUniqueLevelName($"Nível {elevationText}");
            }

            _existingLevels[newLevel.Name] = newLevel;
            return newLevel;
        }
    }
}

