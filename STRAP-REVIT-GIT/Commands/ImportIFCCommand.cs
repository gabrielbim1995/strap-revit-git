using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using StrapRevit.Core;
using StrapRevit.Views;

namespace StrapRevit.Commands
{
    /// <summary>
    /// Comando principal para importar arquivos IFC do STRAP
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ImportIFCCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIApplication uiApp = commandData.Application;
                UIDocument uiDoc = uiApp.ActiveUIDocument;
                Document doc = uiDoc.Document;

                // Verificar se há documento ativo
                if (doc == null)
                {
                    TaskDialog.Show("STRAP-REVIT", "Nenhum documento do Revit está aberto.");
                    return Result.Failed;
                }

                // Abrir diálogo para selecionar arquivo IFC
                string ifcPath = SelectIFCFile();
                if (string.IsNullOrEmpty(ifcPath))
                    return Result.Cancelled;

                // Abrir formulário de configurações
                using (var configForm = new ImportConfigurationForm(doc))
                {
                    if (configForm.ShowDialog() != DialogResult.OK)
                        return Result.Cancelled;

                    // Executar importação com configurações
                    var config = configForm.GetConfiguration();
                    var processor = new IFCProcessor(doc);
                    
                    // Mostrar progresso
                    using (var progressForm = new ImportProgressForm())
                    {
                        progressForm.Show();
                        
                        var result = processor.ProcessIFCFile(ifcPath, config, progressForm);
                        
                        progressForm.Close();
                        
                        // Mostrar relatório final
                        ShowImportReport(result);
                        
                        return result.Success ? Result.Succeeded : Result.Failed;
                    }
                }
            }
            catch (Exception ex)
            {
                message = $"Erro ao importar IFC: {ex.Message}";
                TaskDialog.Show("STRAP-REVIT", message);
                return Result.Failed;
            }
        }

        /// <summary>
        /// Abre diálogo para selecionar arquivo IFC
        /// </summary>
        private string SelectIFCFile()
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "Selecionar arquivo IFC do STRAP";
                dialog.Filter = "Arquivos IFC (*.ifc)|*.ifc|Todos os arquivos (*.*)|*.*";
                dialog.RestoreDirectory = true;

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    return dialog.FileName;
                }
            }
            return null;
        }

        /// <summary>
        /// Mostra relatório de importação
        /// </summary>
        private void ShowImportReport(ImportResult result)
        {
            var dialog = new TaskDialog("STRAP-REVIT - Relatório de Importação")
            {
                MainInstruction = result.Success ? "Importação concluída com sucesso!" : "Importação concluída com avisos",
                MainContent = GenerateReportContent(result),
                CommonButtons = TaskDialogCommonButtons.Ok
            };

            if (result.Warnings.Any())
            {
                dialog.ExpandedContent = "Avisos:\n" + string.Join("\n", result.Warnings);
            }

            dialog.Show();
        }

        /// <summary>
        /// Gera conteúdo do relatório
        /// </summary>
        private string GenerateReportContent(ImportResult result)
        {
            return $@"Arquivo: {result.FileName}
Tempo de processamento: {result.ProcessingTime:F1} segundos

Elementos processados:
• Vigas: {result.BeamCount} ({result.CreatedBeams} criadas, {result.UpdatedBeams} atualizadas)
• Pilares: {result.ColumnCount} ({result.CreatedColumns} criados, {result.UpdatedColumns} atualizados)
• Lajes: {result.SlabCount} ({result.CreatedSlabs} criadas, {result.UpdatedSlabs} atualizadas)
• Fundações: {result.FoundationCount} ({result.CreatedFoundations} criadas, {result.UpdatedFoundations} atualizadas)

Total: {result.TotalElements} elementos
• Criados: {result.TotalCreated}
• Atualizados: {result.TotalUpdated}
• Órfãos: {result.OrphanCount}

Materiais criados: {result.MaterialsCreated}
Níveis criados: {result.LevelsCreated}
Tipos criados: {result.TypesCreated}";
        }
    }
}

