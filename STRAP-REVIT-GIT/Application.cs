using System;
using System.Reflection;
using System.Windows.Media.Imaging;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using System.Windows.Media;

namespace StrapRevit
{
    /// <summary>
    /// Classe principal de aplicação do plugin STRAP-REVIT para integração com o Ribbon do Revit
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class Application : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                CreateRibbonPanel(application);
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Erro", $"Falha ao carregar STRAP-REVIT: {ex.Message}");
                return Result.Failed;
            }
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }

        /// <summary>
        /// Cria o painel no Ribbon do Revit
        /// </summary>
        private void CreateRibbonPanel(UIControlledApplication application)
        {
            // Nome da aba do ribbon
            string tabName = "STRAP-REVIT";

            // Verificar se a aba já existe, se não, criá-la
            try
            {
                application.CreateRibbonTab(tabName);
            }
            catch (Autodesk.Revit.Exceptions.ArgumentException)
            {
                // Aba já existe, ignorar erro
            }

            // Criar painel principal
            var panel = application.CreateRibbonPanel(tabName, "STRAP Tools");

            // Adicionar comando de importação IFC
            CreateImportIFCButton(panel);
        }

        /// <summary>
        /// Cria botão de importação IFC
        /// </summary>
        private void CreateImportIFCButton(RibbonPanel panel)
        {
            var buttonData = new PushButtonData(
                "StrapRevit_ImportIFC",
                "Importar\nIFC STRAP",
                Assembly.GetExecutingAssembly().Location,
                "StrapRevit.Commands.ImportIFCCommand"
            );

            buttonData.ToolTip = "Importar arquivo IFC do STRAP";
            buttonData.LongDescription = 
                "Importa arquivo IFC exportado pelo STRAP e cria elementos nativos do Revit:\n\n" +
                "• Vigas → Structural Framing\n" +
                "• Pilares → Structural Columns\n" +
                "• Lajes → Floors\n" +
                "• Fundações → Structural Foundations\n\n" +
                "Características:\n" +
                "• Elementos nativos e paramétricos (não DirectShapes)\n" +
                "• Atualização incremental baseada em GUIDs\n" +
                "• Criação automática de níveis e materiais\n" +
                "• Rastreabilidade completa com parâmetros IFC\n" +
                "• Mapeamento inteligente de perfis e dimensões";

            var button = panel.AddItem(buttonData) as PushButton;

            // Definir ícone
            button.LargeImage = CreateIFCIcon();
            button.Image = CreateSmallIFCIcon();
        }

        /// <summary>
        /// Cria ícone grande para o botão IFC
        /// </summary>
        private BitmapImage CreateIFCIcon()
        {
            try
            {
                var rtb = new RenderTargetBitmap(32, 32, 96, 96, PixelFormats.Pbgra32);
                var drawingVisual = new DrawingVisual();
                
                using (var drawingContext = drawingVisual.RenderOpen())
                {
                    // Fundo azul STRAP
                    var backgroundBrush = new SolidColorBrush(Color.FromRgb(0, 120, 215));
                    var backgroundRect = new System.Windows.Rect(2, 2, 28, 28);
                    drawingContext.DrawRoundedRectangle(backgroundBrush, null, backgroundRect, 3, 3);
                    
                    // Texto IFC em branco
                    var typeface = new Typeface("Arial");
                    var text = new FormattedText("IFC", 
                        System.Globalization.CultureInfo.CurrentCulture,
                        System.Windows.FlowDirection.LeftToRight, 
                        typeface, 
                        14, 
                        System.Windows.Media.Brushes.White,
                        VisualTreeHelper.GetDpi(drawingVisual).PixelsPerDip);
                    
                    text.TextAlignment = System.Windows.TextAlignment.Center;
                    drawingContext.DrawText(text, new System.Windows.Point(16, 8));
                }
                
                rtb.Render(drawingVisual);

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rtb));

                using (var stream = new System.IO.MemoryStream())
                {
                    encoder.Save(stream);
                    stream.Position = 0;

                    var bitmapImage = new BitmapImage();
                    bitmapImage.BeginInit();
                    bitmapImage.StreamSource = stream;
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.EndInit();
                    bitmapImage.Freeze();

                    return bitmapImage;
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Cria ícone pequeno para o botão IFC
        /// </summary>
        private BitmapImage CreateSmallIFCIcon()
        {
            try
            {
                var rtb = new RenderTargetBitmap(16, 16, 96, 96, PixelFormats.Pbgra32);
                var drawingVisual = new DrawingVisual();
                
                using (var drawingContext = drawingVisual.RenderOpen())
                {
                    // Fundo azul
                    var backgroundBrush = new SolidColorBrush(Color.FromRgb(0, 120, 215));
                    var backgroundRect = new System.Windows.Rect(1, 1, 14, 14);
                    drawingContext.DrawRoundedRectangle(backgroundBrush, null, backgroundRect, 2, 2);
                    
                    // Texto IFC
                    var typeface = new Typeface("Arial");
                    var text = new FormattedText("IFC", 
                        System.Globalization.CultureInfo.CurrentCulture,
                        System.Windows.FlowDirection.LeftToRight, 
                        typeface, 
                        7, 
                        System.Windows.Media.Brushes.White,
                        VisualTreeHelper.GetDpi(drawingVisual).PixelsPerDip);
                    
                    text.TextAlignment = System.Windows.TextAlignment.Center;
                    drawingContext.DrawText(text, new System.Windows.Point(8, 4));
                }
                
                rtb.Render(drawingVisual);

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rtb));

                using (var stream = new System.IO.MemoryStream())
                {
                    encoder.Save(stream);
                    stream.Position = 0;

                    var bitmapImage = new BitmapImage();
                    bitmapImage.BeginInit();
                    bitmapImage.StreamSource = stream;
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.EndInit();
                    bitmapImage.Freeze();

                    return bitmapImage;
                }
            }
            catch
            {
                return null;
            }
        }
    }
}