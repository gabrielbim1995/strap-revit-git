using System;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using StrapRevit.Core;

namespace StrapRevit.Views
{
    /// <summary>
    /// Formulário de configuração de importação IFC
    /// </summary>
    public partial class ImportConfigurationForm : System.Windows.Forms.Form
    {
        private readonly Document _doc;
        private CheckBox _chkCreateLevels;
        private CheckBox _chkCreateMaterials;
        private CheckBox _chkUpdateExisting;
        private ComboBox _cboPhase;
        private ComboBox _cboWorkset;
        private TextBox _txtFamilyPath;
        private Button _btnBrowseFamilies;
        private Button _btnOK;
        private Button _btnCancel;

        public ImportConfigurationForm(Document doc)
        {
            _doc = doc;
            InitializeComponent();
            LoadPhases();
            LoadWorksets();
        }

        /// <summary>
        /// Inicializa componentes do formulário
        /// </summary>
        private void InitializeComponent()
        {
            Text = "STRAP-REVIT - Configurações de Importação IFC";
            Size = new System.Drawing.Size(500, 400);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            var lblTitle = new Label
            {
                Text = "Configurações de Importação IFC do STRAP",
                Font = new System.Drawing.Font("Arial", 12, System.Drawing.FontStyle.Bold),
                Location = new System.Drawing.Point(20, 20),
                Size = new System.Drawing.Size(460, 25)
            };

            // Opções de criação automática
            var grpCreation = new GroupBox
            {
                Text = "Criação Automática",
                Location = new System.Drawing.Point(20, 60),
                Size = new System.Drawing.Size(440, 100)
            };

            _chkCreateLevels = new CheckBox
            {
                Text = "Criar níveis automaticamente quando não existirem",
                Location = new System.Drawing.Point(15, 25),
                Size = new System.Drawing.Size(400, 20),
                Checked = true
            };

            _chkCreateMaterials = new CheckBox
            {
                Text = "Criar materiais automaticamente quando não existirem",
                Location = new System.Drawing.Point(15, 50),
                Size = new System.Drawing.Size(400, 20),
                Checked = true
            };

            _chkUpdateExisting = new CheckBox
            {
                Text = "Atualizar elementos existentes (baseado no GUID IFC)",
                Location = new System.Drawing.Point(15, 75),
                Size = new System.Drawing.Size(400, 20),
                Checked = true
            };

            grpCreation.Controls.AddRange(new System.Windows.Forms.Control[] { _chkCreateLevels, _chkCreateMaterials, _chkUpdateExisting });

            // Configurações de projeto
            var grpProject = new GroupBox
            {
                Text = "Configurações de Projeto",
                Location = new System.Drawing.Point(20, 170),
                Size = new System.Drawing.Size(440, 130)
            };

            var lblPhase = new Label
            {
                Text = "Fase padrão:",
                Location = new System.Drawing.Point(15, 30),
                Size = new System.Drawing.Size(100, 20)
            };

            _cboPhase = new ComboBox
            {
                Location = new System.Drawing.Point(120, 27),
                Size = new System.Drawing.Size(300, 25),
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            var lblWorkset = new Label
            {
                Text = "Workset padrão:",
                Location = new System.Drawing.Point(15, 60),
                Size = new System.Drawing.Size(100, 20)
            };

            _cboWorkset = new ComboBox
            {
                Location = new System.Drawing.Point(120, 57),
                Size = new System.Drawing.Size(300, 25),
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            var lblFamilyPath = new Label
            {
                Text = "Pasta de famílias:",
                Location = new System.Drawing.Point(15, 90),
                Size = new System.Drawing.Size(100, 20)
            };

            _txtFamilyPath = new TextBox
            {
                Location = new System.Drawing.Point(120, 87),
                Size = new System.Drawing.Size(250, 25),
                ReadOnly = true
            };

            _btnBrowseFamilies = new Button
            {
                Text = "...",
                Location = new System.Drawing.Point(375, 87),
                Size = new System.Drawing.Size(45, 23)
            };
            _btnBrowseFamilies.Click += BtnBrowseFamilies_Click;

            grpProject.Controls.AddRange(new System.Windows.Forms.Control[] 
            { 
                lblPhase, _cboPhase, 
                lblWorkset, _cboWorkset,
                lblFamilyPath, _txtFamilyPath, _btnBrowseFamilies
            });

            // Botões
            _btnOK = new Button
            {
                Text = "OK",
                Location = new System.Drawing.Point(290, 320),
                Size = new System.Drawing.Size(85, 30),
                DialogResult = DialogResult.OK
            };

            _btnCancel = new Button
            {
                Text = "Cancelar",
                Location = new System.Drawing.Point(385, 320),
                Size = new System.Drawing.Size(85, 30),
                DialogResult = DialogResult.Cancel
            };

            // Adicionar controles ao formulário
            Controls.AddRange(new System.Windows.Forms.Control[] 
            { 
                lblTitle, 
                grpCreation, 
                grpProject, 
                _btnOK, 
                _btnCancel 
            });

            AcceptButton = _btnOK;
            CancelButton = _btnCancel;
        }

        /// <summary>
        /// Carrega fases do projeto
        /// </summary>
        private void LoadPhases()
        {
            _cboPhase.Items.Add("(Nenhuma)");
            
            var phases = new FilteredElementCollector(_doc)
                .OfClass(typeof(Phase))
                .Cast<Phase>()
                .OrderBy(p => p.Name);

            foreach (var phase in phases)
            {
                _cboPhase.Items.Add(new PhaseItem { Phase = phase, Name = phase.Name });
            }

            _cboPhase.SelectedIndex = 0;
        }

        /// <summary>
        /// Carrega worksets do projeto
        /// </summary>
        private void LoadWorksets()
        {
            _cboWorkset.Items.Add("(Workset atual)");
            
            if (_doc.IsWorkshared)
            {
                var worksets = new FilteredWorksetCollector(_doc)
                    .OfKind(WorksetKind.UserWorkset)
                    .OrderBy(w => w.Name);

                foreach (var workset in worksets)
                {
                    _cboWorkset.Items.Add(new WorksetItem 
                    { 
                        Workset = workset, 
                        Name = workset.Name 
                    });
                }
            }

            _cboWorkset.SelectedIndex = 0;
            _cboWorkset.Enabled = _doc.IsWorkshared;
        }

        /// <summary>
        /// Evento de procurar pasta de famílias
        /// </summary>
        private void BtnBrowseFamilies_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Selecionar pasta de famílias do STRAP";
                dialog.ShowNewFolderButton = false;

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    _txtFamilyPath.Text = dialog.SelectedPath;
                }
            }
        }

        /// <summary>
        /// Obtém configuração selecionada
        /// </summary>
        public ImportConfiguration GetConfiguration()
        {
            var config = new ImportConfiguration
            {
                CreateLevels = _chkCreateLevels.Checked,
                CreateMaterials = _chkCreateMaterials.Checked,
                UpdateExisting = _chkUpdateExisting.Checked,
                FamilyLoadPath = _txtFamilyPath.Text
            };

            // Fase
            if (_cboPhase.SelectedItem is PhaseItem phaseItem)
            {
                config.DefaultPhaseId = phaseItem.Phase.Id;
            }

            // Workset
            if (_cboWorkset.SelectedItem is WorksetItem worksetItem)
            {
                config.DefaultWorksetId = worksetItem.Workset.Id.IntegerValue;
            }

            return config;
        }

        /// <summary>
        /// Item de fase para ComboBox
        /// </summary>
        private class PhaseItem
        {
            public Phase Phase { get; set; }
            public string Name { get; set; }
            public override string ToString() => Name;
        }

        /// <summary>
        /// Item de workset para ComboBox
        /// </summary>
        private class WorksetItem
        {
            public Workset Workset { get; set; }
            public string Name { get; set; }
            public override string ToString() => Name;
        }
    }
}
