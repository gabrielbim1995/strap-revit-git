using System;
using System.Windows.Forms;

namespace StrapRevit.Views
{
    /// <summary>
    /// Formulário de progresso da importação
    /// </summary>
    public partial class ImportProgressForm : System.Windows.Forms.Form
    {
        private ProgressBar _progressBar;
        private Label _lblStatus;
        private Label _lblDetails;
        private ListBox _lstLog;

        public ImportProgressForm()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Inicializa componentes do formulário
        /// </summary>
        private void InitializeComponent()
        {
            Text = "STRAP-REVIT - Importando IFC";
            Size = new System.Drawing.Size(600, 400);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ControlBox = false; // Não permitir fechar durante importação

            var lblTitle = new Label
            {
                Text = "Importando arquivo IFC do STRAP",
                Font = new System.Drawing.Font("Arial", 12, System.Drawing.FontStyle.Bold),
                Location = new System.Drawing.Point(20, 20),
                Size = new System.Drawing.Size(560, 25)
            };

            _lblStatus = new Label
            {
                Text = "Iniciando...",
                Location = new System.Drawing.Point(20, 60),
                Size = new System.Drawing.Size(560, 20)
            };

            _progressBar = new ProgressBar
            {
                Location = new System.Drawing.Point(20, 90),
                Size = new System.Drawing.Size(560, 30),
                Style = ProgressBarStyle.Continuous,
                Minimum = 0,
                Maximum = 100
            };

            _lblDetails = new Label
            {
                Text = "",
                Location = new System.Drawing.Point(20, 130),
                Size = new System.Drawing.Size(560, 20),
                ForeColor = System.Drawing.Color.Gray
            };

            var lblLog = new Label
            {
                Text = "Log de processamento:",
                Location = new System.Drawing.Point(20, 160),
                Size = new System.Drawing.Size(200, 20)
            };

            _lstLog = new ListBox
            {
                Location = new System.Drawing.Point(20, 185),
                Size = new System.Drawing.Size(560, 150),
                ScrollAlwaysVisible = true,
                Font = new System.Drawing.Font("Consolas", 8)
            };

            Controls.AddRange(new System.Windows.Forms.Control[] 
            { 
                lblTitle, 
                _lblStatus, 
                _progressBar, 
                _lblDetails,
                lblLog,
                _lstLog
            });
        }

        /// <summary>
        /// Atualiza progresso
        /// </summary>
        public void UpdateProgress(string status, int percentage)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => UpdateProgress(status, percentage)));
                return;
            }

            _lblStatus.Text = status;
            _progressBar.Value = Math.Min(Math.Max(percentage, 0), 100);
            
            // Adicionar ao log
            AddToLog($"[{DateTime.Now:HH:mm:ss}] {status}");
            
            System.Windows.Forms.Application.DoEvents();
        }

        /// <summary>
        /// Atualiza detalhes
        /// </summary>
        public void UpdateDetails(string details)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => UpdateDetails(details)));
                return;
            }

            _lblDetails.Text = details;
            System.Windows.Forms.Application.DoEvents();
        }

        /// <summary>
        /// Adiciona mensagem ao log
        /// </summary>
        public void AddToLog(string message)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => AddToLog(message)));
                return;
            }

            _lstLog.Items.Add(message);
            
            // Auto-scroll para última mensagem
            _lstLog.SelectedIndex = _lstLog.Items.Count - 1;
            _lstLog.SelectedIndex = -1;
            
            System.Windows.Forms.Application.DoEvents();
        }

        /// <summary>
        /// Define se pode fechar o formulário
        /// </summary>
        public void SetClosable(bool closable)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => SetClosable(closable)));
                return;
            }

            ControlBox = closable;
            
            if (closable)
            {
                // Adicionar botão fechar
                var btnClose = new Button
                {
                    Text = "Fechar",
                    Location = new System.Drawing.Point(495, 345),
                    Size = new System.Drawing.Size(85, 30),
                    DialogResult = DialogResult.OK
                };
                
                Controls.Add(btnClose);
                AcceptButton = btnClose;
            }
        }

        /// <summary>
        /// Marca como concluído
        /// </summary>
        public void SetCompleted(bool success)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => SetCompleted(success)));
                return;
            }

            _progressBar.Value = 100;
            
            if (success)
            {
                _lblStatus.Text = "Importação concluída com sucesso!";
                _lblStatus.ForeColor = System.Drawing.Color.Green;
            }
            else
            {
                _lblStatus.Text = "Importação concluída com avisos.";
                _lblStatus.ForeColor = System.Drawing.Color.Orange;
            }
            
            SetClosable(true);
        }
    }
}
