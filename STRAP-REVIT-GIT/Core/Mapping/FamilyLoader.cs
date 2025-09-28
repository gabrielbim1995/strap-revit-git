using System;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;

namespace StrapRevit.Core.Mapping
{
    /// <summary>
    /// Carregador de famílias do Revit
    /// </summary>
    public class FamilyLoader
    {
        private readonly Document _doc;
        private readonly string _familyBasePath;

        public FamilyLoader(Document doc)
        {
            _doc = doc;
            _familyBasePath = GetFamilyBasePath();
        }

        /// <summary>
        /// Obtém caminho base das famílias
        /// </summary>
        private string GetFamilyBasePath()
        {
            // Tentar múltiplos caminhos possíveis
            var possiblePaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), 
                    "Autodesk", "RVT 2024", "Libraries", "Brazil"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), 
                    "Autodesk", "RVT 2024", "Libraries", "US Imperial"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), 
                    "Autodesk", "Revit 2024", "Family Templates"),
                @"C:\ProgramData\Autodesk\RVT 2024\Libraries\Brazil",
                @"C:\ProgramData\Autodesk\RVT 2024\Libraries\US Imperial"
            };

            return possiblePaths.FirstOrDefault(Directory.Exists) ?? "";
        }

        /// <summary>
        /// Carrega família de viga estrutural
        /// </summary>
        public Family LoadStructuralFramingFamily(string familyName)
        {
            var searchPaths = new[]
            {
                Path.Combine(_familyBasePath, "Structural Framing"),
                Path.Combine(_familyBasePath, "Estrutura", "Vigas"),
                Path.Combine(_familyBasePath, "Structural", "Framing")
            };

            return LoadFamilyFromPaths(familyName, searchPaths);
        }

        /// <summary>
        /// Carrega família de pilar estrutural
        /// </summary>
        public Family LoadStructuralColumnFamily(string familyName)
        {
            var searchPaths = new[]
            {
                Path.Combine(_familyBasePath, "Structural Columns"),
                Path.Combine(_familyBasePath, "Estrutura", "Pilares"),
                Path.Combine(_familyBasePath, "Structural", "Columns")
            };

            return LoadFamilyFromPaths(familyName, searchPaths);
        }

        /// <summary>
        /// Carrega família de fundação estrutural
        /// </summary>
        public Family LoadStructuralFoundationFamily(string familyName)
        {
            var searchPaths = new[]
            {
                Path.Combine(_familyBasePath, "Structural Foundations"),
                Path.Combine(_familyBasePath, "Estrutura", "Fundações"),
                Path.Combine(_familyBasePath, "Structural", "Foundations")
            };

            return LoadFamilyFromPaths(familyName, searchPaths);
        }

        /// <summary>
        /// Carrega família a partir de múltiplos caminhos
        /// </summary>
        private Family LoadFamilyFromPaths(string familyName, string[] searchPaths)
        {
            foreach (var basePath in searchPaths)
            {
                if (!Directory.Exists(basePath))
                    continue;

                // Buscar arquivo .rfa
                var familyFiles = Directory.GetFiles(basePath, "*.rfa", SearchOption.AllDirectories)
                    .Where(f => Path.GetFileNameWithoutExtension(f).Contains(familyName))
                    .ToList();

                foreach (var familyFile in familyFiles)
                {
                    try
                    {
                        Family family;
                        if (_doc.LoadFamily(familyFile, new FamilyLoadOptions(), out family))
                        {
                            return family;
                        }
                    }
                    catch
                    {
                        // Continuar tentando outros arquivos
                    }
                }
            }

            // Tentar criar família básica se não encontrar
            return CreateBasicFamily(familyName);
        }

        /// <summary>
        /// Cria família básica se não encontrar arquivo
        /// </summary>
        private Family CreateBasicFamily(string familyName)
        {
            // Por enquanto retornar null
            // Em uma implementação completa, poderíamos criar famílias básicas programaticamente
            return null;
        }

        /// <summary>
        /// Opções de carregamento de família
        /// </summary>
        private class FamilyLoadOptions : IFamilyLoadOptions
        {
            public bool OnFamilyFound(bool familyInUse, out bool overwriteParameterValues)
            {
                overwriteParameterValues = false;
                return !familyInUse; // Só sobrescrever se não estiver em uso
            }

            public bool OnSharedFamilyFound(Family sharedFamily, bool familyInUse, out FamilySource source, out bool overwriteParameterValues)
            {
                source = FamilySource.Family;
                overwriteParameterValues = false;
                return !familyInUse;
            }
        }
    }
}

