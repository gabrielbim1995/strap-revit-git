using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using StrapRevit.Core.IFC;

namespace StrapRevit.Core.Parameters
{
    /// <summary>
    /// Gerenciador de parâmetros para rastreabilidade IFC
    /// </summary>
    public class ParameterManager
    {
        private readonly Document _doc;
        private readonly Dictionary<string, ExternalDefinition> _sharedParameters;
        private const string PARAMETER_GROUP_NAME = "STRAP-IFC";
        
        // Nomes dos parâmetros
        private const string PARAM_IFC_GUID = "IFC_GUID";
        private const string PARAM_IFC_CLASS = "IFC_Class";
        private const string PARAM_IFC_FILE = "IFC_SourceFile";
        private const string PARAM_IFC_TIMESTAMP = "IFC_LastSync";
        private const string PARAM_IFC_ORPHAN = "IFC_Orphan";
        private const string PARAM_IFC_TYPE = "IFC_TypeName";
        private const string PARAM_IFC_MATERIAL = "IFC_Material";
        private const string PARAM_FOUNDATION_TYPE = "Foundation_Type";
        private const string PARAM_IFC_FALLBACK = "IFC_FallbackInfo";

        public ParameterManager(Document doc)
        {
            _doc = doc;
            _sharedParameters = new Dictionary<string, ExternalDefinition>();
            EnsureSharedParameters();
        }

        /// <summary>
        /// Garante que os parâmetros compartilhados existam
        /// </summary>
        private void EnsureSharedParameters()
        {
            var app = _doc.Application;
            var sharedParamsFile = GetOrCreateSharedParameterFile(app);
            
            if (sharedParamsFile == null)
                return;

            var group = GetOrCreateParameterGroup(sharedParamsFile);
            
            // Criar definições de parâmetros
            CreateParameterDefinition(group, PARAM_IFC_GUID, SpecTypeId.String.Text);
            CreateParameterDefinition(group, PARAM_IFC_CLASS, SpecTypeId.String.Text);
            CreateParameterDefinition(group, PARAM_IFC_FILE, SpecTypeId.String.Text);
            CreateParameterDefinition(group, PARAM_IFC_TIMESTAMP, SpecTypeId.String.Text);
            CreateParameterDefinition(group, PARAM_IFC_ORPHAN, SpecTypeId.Boolean.YesNo);
            CreateParameterDefinition(group, PARAM_IFC_TYPE, SpecTypeId.String.Text);
            CreateParameterDefinition(group, PARAM_IFC_MATERIAL, SpecTypeId.String.Text);
            CreateParameterDefinition(group, PARAM_FOUNDATION_TYPE, SpecTypeId.String.Text);
            CreateParameterDefinition(group, PARAM_IFC_FALLBACK, SpecTypeId.String.Text);

            // Carregar definições
            LoadParameterDefinitions(group);
            
            // Vincular aos elementos
            BindParametersToCategories();
        }

        /// <summary>
        /// Obtém ou cria arquivo de parâmetros compartilhados
        /// </summary>
        private DefinitionFile GetOrCreateSharedParameterFile(Autodesk.Revit.ApplicationServices.Application app)
        {
            try
            {
                var sharedParamsFilename = app.SharedParametersFilename;
                
                if (string.IsNullOrEmpty(sharedParamsFilename) || !System.IO.File.Exists(sharedParamsFilename))
                {
                    // Criar novo arquivo
                    var tempPath = System.IO.Path.GetTempPath();
                    sharedParamsFilename = System.IO.Path.Combine(tempPath, "STRAP_SharedParams.txt");
                    
                    if (!System.IO.File.Exists(sharedParamsFilename))
                    {
                        using (var file = System.IO.File.Create(sharedParamsFilename))
                        {
                            // Arquivo vazio será preenchido pelo Revit
                        }
                    }
                    
                    app.SharedParametersFilename = sharedParamsFilename;
                }

                return app.OpenSharedParameterFile();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Obtém ou cria grupo de parâmetros
        /// </summary>
        private DefinitionGroup GetOrCreateParameterGroup(DefinitionFile file)
        {
            var group = file.Groups.get_Item(PARAMETER_GROUP_NAME);
            
            if (group == null)
            {
                group = file.Groups.Create(PARAMETER_GROUP_NAME);
            }

            return group;
        }

        /// <summary>
        /// Cria definição de parâmetro
        /// </summary>
        private void CreateParameterDefinition(DefinitionGroup group, string name, ForgeTypeId typeId)
        {
            var existingDef = group.Definitions.get_Item(name);
            
            if (existingDef == null)
            {
                var options = new ExternalDefinitionCreationOptions(name, typeId);
                options.UserModifiable = false;
                options.Description = $"Parâmetro IFC STRAP: {name}";
                
                group.Definitions.Create(options);
            }
        }

        /// <summary>
        /// Carrega definições de parâmetros
        /// </summary>
        private void LoadParameterDefinitions(DefinitionGroup group)
        {
            foreach (Definition def in group.Definitions)
            {
                if (def is ExternalDefinition extDef)
                {
                    _sharedParameters[def.Name] = extDef;
                }
            }
        }

        /// <summary>
        /// Vincula parâmetros às categorias
        /// </summary>
        private void BindParametersToCategories()
        {
            var categories = new[]
            {
                Category.GetCategory(_doc, BuiltInCategory.OST_StructuralFraming),
                Category.GetCategory(_doc, BuiltInCategory.OST_StructuralColumns),
                Category.GetCategory(_doc, BuiltInCategory.OST_Floors),
                Category.GetCategory(_doc, BuiltInCategory.OST_StructuralFoundation)
            };

            var categorySet = new CategorySet();
            foreach (var cat in categories.Where(c => c != null))
            {
                categorySet.Insert(cat);
            }

            var binding = new InstanceBinding(categorySet);

            foreach (var paramDef in _sharedParameters.Values)
            {
                try
                {
                    _doc.ParameterBindings.Insert(paramDef, binding, GroupTypeId.Ifc);
                }
                catch
                {
                    // Parâmetro já pode estar vinculado
                }
            }
        }

        /// <summary>
        /// Define parâmetros IFC em um elemento
        /// </summary>
        public void SetIFCParameters(Element element, IFCElement ifcElement)
        {
            SetParameterValue(element, PARAM_IFC_GUID, ifcElement.GlobalId);
            SetParameterValue(element, PARAM_IFC_CLASS, ifcElement.GetType().Name);
            SetParameterValue(element, PARAM_IFC_TYPE, ifcElement.TypeName ?? "");
            SetParameterValue(element, PARAM_IFC_MATERIAL, ifcElement.Material ?? "");
            SetParameterValue(element, PARAM_IFC_TIMESTAMP, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            SetParameterValue(element, PARAM_IFC_ORPHAN, false);
        }

        /// <summary>
        /// Atualiza parâmetros IFC em um elemento existente
        /// </summary>
        public void UpdateIFCParameters(Element element, IFCElement ifcElement)
        {
            SetParameterValue(element, PARAM_IFC_TYPE, ifcElement.TypeName ?? "");
            SetParameterValue(element, PARAM_IFC_MATERIAL, ifcElement.Material ?? "");
            SetParameterValue(element, PARAM_IFC_TIMESTAMP, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            SetParameterValue(element, PARAM_IFC_ORPHAN, false);
        }

        /// <summary>
        /// Marca elemento como órfão
        /// </summary>
        public void MarkAsOrphan(Element element)
        {
            SetParameterValue(element, PARAM_IFC_ORPHAN, true);
            SetParameterValue(element, PARAM_IFC_TIMESTAMP, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        }

        /// <summary>
        /// Define tipo de fundação
        /// </summary>
        public void SetFoundationType(Element element, string foundationType)
        {
            SetParameterValue(element, PARAM_FOUNDATION_TYPE, foundationType);
        }

        /// <summary>
        /// Define informações de fallback
        /// </summary>
        public void SetFallbackInfo(Element element, string intendedFamily, string usedFamily)
        {
            var fallbackInfo = $"Família pretendida: {intendedFamily} | Família usada: {usedFamily}";
            SetParameterValue(element, PARAM_IFC_FALLBACK, fallbackInfo);
        }

        /// <summary>
        /// Obtém GUID IFC de um elemento
        /// </summary>
        public string GetIFCGuid(Element element)
        {
            var param = element.LookupParameter(PARAM_IFC_GUID);
            return param?.AsString() ?? "";
        }

        /// <summary>
        /// Define informações da última importação
        /// </summary>
        public void SetLastImportInfo(Element projectInfo, string ifcPath, DateTime timestamp)
        {
            SetParameterValue(projectInfo, PARAM_IFC_FILE, ifcPath);
            SetParameterValue(projectInfo, PARAM_IFC_TIMESTAMP, timestamp.ToString("yyyy-MM-dd HH:mm:ss"));
        }

        /// <summary>
        /// Define valor de parâmetro
        /// </summary>
        private void SetParameterValue(Element element, string paramName, object value)
        {
            var param = element.LookupParameter(paramName);
            
            if (param == null || param.IsReadOnly)
                return;

            try
            {
                switch (value)
                {
                    case string strValue:
                        param.Set(strValue);
                        break;
                        
                    case bool boolValue:
                        param.Set(boolValue ? 1 : 0);
                        break;
                        
                    case int intValue:
                        param.Set(intValue);
                        break;
                        
                    case double doubleValue:
                        param.Set(doubleValue);
                        break;
                        
                    case ElementId idValue:
                        param.Set(idValue);
                        break;
                }
            }
            catch
            {
                // Ignorar erros ao definir parâmetros
            }
        }

        /// <summary>
        /// Cria parâmetros de projeto se não existirem
        /// </summary>
        public void EnsureProjectParameters()
        {
            // Esta é uma abordagem alternativa usando parâmetros de projeto
            // em vez de parâmetros compartilhados, se preferir
            
            var parameterNames = new[]
            {
                PARAM_IFC_GUID,
                PARAM_IFC_CLASS,
                PARAM_IFC_FILE,
                PARAM_IFC_TIMESTAMP,
                PARAM_IFC_ORPHAN,
                PARAM_IFC_TYPE,
                PARAM_IFC_MATERIAL,
                PARAM_FOUNDATION_TYPE
            };

            foreach (var paramName in parameterNames)
            {
                // Verificar se já existe
                var existingParam = new FilteredElementCollector(_doc)
                    .OfClass(typeof(ParameterElement))
                    .Cast<ParameterElement>()
                    .FirstOrDefault(p => p.Name == paramName);

                if (existingParam == null)
                {
                    // Criar parâmetro de projeto
                    CreateProjectParameter(paramName);
                }
            }
        }

        /// <summary>
        /// Cria parâmetro de projeto
        /// </summary>
        private void CreateProjectParameter(string name)
        {
            // Esta funcionalidade requer API mais avançada
            // Por enquanto, usamos apenas parâmetros compartilhados
        }
    }
}
