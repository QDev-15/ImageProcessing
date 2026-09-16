namespace WinFormsDemo
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            labelFormat = new Label();
            comboBoxFormat = new ComboBox();
            checkBoxSharpen = new CheckBox();
            labelSigma = new Label();
            numericUpDownSigma = new NumericUpDown();
            labelStrength = new Label();
            numericUpDownStrength = new NumericUpDown();
            checkBoxDenoise = new CheckBox();
            labelQuality = new Label();
            numericUpDownQuality = new NumericUpDown();
            checkBoxTargetSize = new CheckBox();
            labelTargetKb = new Label();
            numericUpDownTargetKb = new NumericUpDown();
            buttonPickAndProcess = new Button();
            buttonSave = new Button();
            labelBefore = new Label();
            labelAfter = new Label();
            pictureBoxBefore = new HighQualityPictureBox();
            pictureBoxAfter = new HighQualityPictureBox();
            textBoxResult = new TextBox();
            btnRefresh = new Button();
            ((System.ComponentModel.ISupportInitialize)numericUpDownSigma).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numericUpDownStrength).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numericUpDownQuality).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numericUpDownTargetKb).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pictureBoxBefore).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pictureBoxAfter).BeginInit();
            SuspendLayout();
            // 
            // labelFormat
            // 
            labelFormat.AutoSize = true;
            labelFormat.Location = new Point(12, 15);
            labelFormat.Name = "labelFormat";
            labelFormat.Size = new Size(65, 15);
            labelFormat.TabIndex = 0;
            labelFormat.Text = "Dinh dang:";
            // 
            // comboBoxFormat
            // 
            comboBoxFormat.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxFormat.FormattingEnabled = true;
            comboBoxFormat.Items.AddRange(new object[] { "JPEG", "PNG", "WebP" });
            comboBoxFormat.Location = new Point(88, 12);
            comboBoxFormat.Name = "comboBoxFormat";
            comboBoxFormat.Size = new Size(90, 23);
            comboBoxFormat.TabIndex = 1;
            // 
            // checkBoxSharpen
            // 
            checkBoxSharpen.AutoSize = true;
            checkBoxSharpen.Checked = true;
            checkBoxSharpen.CheckState = CheckState.Checked;
            checkBoxSharpen.Location = new Point(196, 14);
            checkBoxSharpen.Name = "checkBoxSharpen";
            checkBoxSharpen.Size = new Size(69, 19);
            checkBoxSharpen.TabIndex = 2;
            checkBoxSharpen.Text = "Lam net";
            checkBoxSharpen.UseVisualStyleBackColor = true;
            // 
            // labelSigma
            // 
            labelSigma.AutoSize = true;
            labelSigma.Location = new Point(286, 15);
            labelSigma.Name = "labelSigma";
            labelSigma.Size = new Size(43, 15);
            labelSigma.TabIndex = 3;
            labelSigma.Text = "Sigma:";
            // 
            // numericUpDownSigma
            // 
            numericUpDownSigma.DecimalPlaces = 1;
            numericUpDownSigma.Increment = new decimal(new int[] { 1, 0, 0, 65536 });
            numericUpDownSigma.Location = new Point(334, 12);
            numericUpDownSigma.Maximum = new decimal(new int[] { 5, 0, 0, 0 });
            numericUpDownSigma.Minimum = new decimal(new int[] { 1, 0, 0, 65536 });
            numericUpDownSigma.Name = "numericUpDownSigma";
            numericUpDownSigma.Size = new Size(55, 23);
            numericUpDownSigma.TabIndex = 4;
            numericUpDownSigma.Value = new decimal(new int[] { 10, 0, 0, 65536 });
            // 
            // labelStrength
            // 
            labelStrength.AutoSize = true;
            labelStrength.Location = new Point(398, 15);
            labelStrength.Name = "labelStrength";
            labelStrength.Size = new Size(55, 15);
            labelStrength.TabIndex = 5;
            labelStrength.Text = "Strength:";
            // 
            // numericUpDownStrength
            // 
            numericUpDownStrength.DecimalPlaces = 1;
            numericUpDownStrength.Increment = new decimal(new int[] { 1, 0, 0, 65536 });
            numericUpDownStrength.Location = new Point(460, 12);
            numericUpDownStrength.Maximum = new decimal(new int[] { 3, 0, 0, 0 });
            numericUpDownStrength.Name = "numericUpDownStrength";
            numericUpDownStrength.Size = new Size(55, 23);
            numericUpDownStrength.TabIndex = 6;
            numericUpDownStrength.Value = new decimal(new int[] { 8, 0, 0, 65536 });
            // 
            // checkBoxDenoise
            // 
            checkBoxDenoise.AutoSize = true;
            checkBoxDenoise.Location = new Point(530, 14);
            checkBoxDenoise.Name = "checkBoxDenoise";
            checkBoxDenoise.Size = new Size(80, 19);
            checkBoxDenoise.TabIndex = 7;
            checkBoxDenoise.Text = "Khu nhieu";
            checkBoxDenoise.UseVisualStyleBackColor = true;
            // 
            // labelQuality
            // 
            labelQuality.AutoSize = true;
            labelQuality.Location = new Point(12, 48);
            labelQuality.Name = "labelQuality";
            labelQuality.Size = new Size(48, 15);
            labelQuality.TabIndex = 8;
            labelQuality.Text = "Quality:";
            // 
            // numericUpDownQuality
            // 
            numericUpDownQuality.Location = new Point(68, 45);
            numericUpDownQuality.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            numericUpDownQuality.Name = "numericUpDownQuality";
            numericUpDownQuality.Size = new Size(55, 23);
            numericUpDownQuality.TabIndex = 9;
            numericUpDownQuality.Value = new decimal(new int[] { 85, 0, 0, 0 });
            // 
            // checkBoxTargetSize
            // 
            checkBoxTargetSize.AutoSize = true;
            checkBoxTargetSize.Location = new Point(140, 47);
            checkBoxTargetSize.Name = "checkBoxTargetSize";
            checkBoxTargetSize.Size = new Size(142, 19);
            checkBoxTargetSize.TabIndex = 10;
            checkBoxTargetSize.Text = "Nen theo KB muc tieu";
            checkBoxTargetSize.UseVisualStyleBackColor = true;
            checkBoxTargetSize.CheckedChanged += CheckBoxTargetSize_CheckedChanged;
            // 
            // labelTargetKb
            // 
            labelTargetKb.AutoSize = true;
            labelTargetKb.Location = new Point(300, 48);
            labelTargetKb.Name = "labelTargetKb";
            labelTargetKb.Size = new Size(60, 15);
            labelTargetKb.TabIndex = 11;
            labelTargetKb.Text = "Target KB:";
            // 
            // numericUpDownTargetKb
            // 
            numericUpDownTargetKb.Enabled = false;
            numericUpDownTargetKb.Location = new Point(368, 45);
            numericUpDownTargetKb.Maximum = new decimal(new int[] { 10000, 0, 0, 0 });
            numericUpDownTargetKb.Minimum = new decimal(new int[] { 5, 0, 0, 0 });
            numericUpDownTargetKb.Name = "numericUpDownTargetKb";
            numericUpDownTargetKb.Size = new Size(65, 23);
            numericUpDownTargetKb.TabIndex = 12;
            numericUpDownTargetKb.Value = new decimal(new int[] { 200, 0, 0, 0 });
            // 
            // buttonPickAndProcess
            // 
            buttonPickAndProcess.Location = new Point(450, 43);
            buttonPickAndProcess.Name = "buttonPickAndProcess";
            buttonPickAndProcess.Size = new Size(150, 28);
            buttonPickAndProcess.TabIndex = 13;
            buttonPickAndProcess.Text = "Chon anh && Xu ly...";
            buttonPickAndProcess.UseVisualStyleBackColor = true;
            buttonPickAndProcess.Click += ButtonPickAndProcess_Click;
            // 
            // buttonSave
            // 
            buttonSave.Enabled = false;
            buttonSave.Location = new Point(838, 40);
            buttonSave.Name = "buttonSave";
            buttonSave.Size = new Size(150, 28);
            buttonSave.TabIndex = 14;
            buttonSave.Text = "Luu ket qua...";
            buttonSave.UseVisualStyleBackColor = true;
            buttonSave.Click += ButtonSave_Click;
            // 
            // labelBefore
            // 
            labelBefore.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            labelBefore.Location = new Point(12, 85);
            labelBefore.Name = "labelBefore";
            labelBefore.Size = new Size(480, 18);
            labelBefore.TabIndex = 15;
            labelBefore.Text = "Truoc";
            labelBefore.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // labelAfter
            // 
            labelAfter.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            labelAfter.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            labelAfter.Location = new Point(508, 85);
            labelAfter.Name = "labelAfter";
            labelAfter.Size = new Size(480, 18);
            labelAfter.TabIndex = 16;
            labelAfter.Text = "Sau";
            labelAfter.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // pictureBoxBefore
            // 
            pictureBoxBefore.BackColor = Color.WhiteSmoke;
            pictureBoxBefore.BorderStyle = BorderStyle.FixedSingle;
            pictureBoxBefore.Location = new Point(12, 105);
            pictureBoxBefore.Name = "pictureBoxBefore";
            pictureBoxBefore.Size = new Size(480, 410);
            pictureBoxBefore.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBoxBefore.TabIndex = 17;
            pictureBoxBefore.TabStop = false;
            // 
            // pictureBoxAfter
            // 
            pictureBoxAfter.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            pictureBoxAfter.BackColor = Color.WhiteSmoke;
            pictureBoxAfter.BorderStyle = BorderStyle.FixedSingle;
            pictureBoxAfter.Location = new Point(508, 105);
            pictureBoxAfter.Name = "pictureBoxAfter";
            pictureBoxAfter.Size = new Size(480, 410);
            pictureBoxAfter.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBoxAfter.TabIndex = 18;
            pictureBoxAfter.TabStop = false;
            // 
            // textBoxResult
            // 
            textBoxResult.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            textBoxResult.Font = new Font("Consolas", 9.5F);
            textBoxResult.Location = new Point(12, 525);
            textBoxResult.Multiline = true;
            textBoxResult.Name = "textBoxResult";
            textBoxResult.ReadOnly = true;
            textBoxResult.ScrollBars = ScrollBars.Vertical;
            textBoxResult.Size = new Size(976, 95);
            textBoxResult.TabIndex = 19;
            // 
            // btnRefresh
            // 
            btnRefresh.Enabled = false;
            btnRefresh.Location = new Point(606, 41);
            btnRefresh.Name = "btnRefresh";
            btnRefresh.Size = new Size(150, 28);
            btnRefresh.TabIndex = 14;
            btnRefresh.Text = "Refresh...";
            btnRefresh.UseVisualStyleBackColor = true;
            btnRefresh.Click += btnRefresh_Click;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1004, 650);
            Controls.Add(textBoxResult);
            Controls.Add(pictureBoxAfter);
            Controls.Add(pictureBoxBefore);
            Controls.Add(labelAfter);
            Controls.Add(labelBefore);
            Controls.Add(btnRefresh);
            Controls.Add(buttonSave);
            Controls.Add(buttonPickAndProcess);
            Controls.Add(numericUpDownTargetKb);
            Controls.Add(labelTargetKb);
            Controls.Add(checkBoxTargetSize);
            Controls.Add(numericUpDownQuality);
            Controls.Add(labelQuality);
            Controls.Add(checkBoxDenoise);
            Controls.Add(numericUpDownStrength);
            Controls.Add(labelStrength);
            Controls.Add(numericUpDownSigma);
            Controls.Add(labelSigma);
            Controls.Add(checkBoxSharpen);
            Controls.Add(comboBoxFormat);
            Controls.Add(labelFormat);
            MinimumSize = new Size(1020, 680);
            Name = "MainForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "ImageProcessing - WinForms Demo";
            ((System.ComponentModel.ISupportInitialize)numericUpDownSigma).EndInit();
            ((System.ComponentModel.ISupportInitialize)numericUpDownStrength).EndInit();
            ((System.ComponentModel.ISupportInitialize)numericUpDownQuality).EndInit();
            ((System.ComponentModel.ISupportInitialize)numericUpDownTargetKb).EndInit();
            ((System.ComponentModel.ISupportInitialize)pictureBoxBefore).EndInit();
            ((System.ComponentModel.ISupportInitialize)pictureBoxAfter).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Label labelFormat;
        private System.Windows.Forms.ComboBox comboBoxFormat;
        private System.Windows.Forms.CheckBox checkBoxSharpen;
        private System.Windows.Forms.Label labelSigma;
        private System.Windows.Forms.NumericUpDown numericUpDownSigma;
        private System.Windows.Forms.Label labelStrength;
        private System.Windows.Forms.NumericUpDown numericUpDownStrength;
        private System.Windows.Forms.CheckBox checkBoxDenoise;
        private System.Windows.Forms.Label labelQuality;
        private System.Windows.Forms.NumericUpDown numericUpDownQuality;
        private System.Windows.Forms.CheckBox checkBoxTargetSize;
        private System.Windows.Forms.Label labelTargetKb;
        private System.Windows.Forms.NumericUpDown numericUpDownTargetKb;
        private System.Windows.Forms.Button buttonPickAndProcess;
        private System.Windows.Forms.Button buttonSave;
        private System.Windows.Forms.Label labelBefore;
        private System.Windows.Forms.Label labelAfter;
        private HighQualityPictureBox pictureBoxBefore;
        private HighQualityPictureBox pictureBoxAfter;
        private System.Windows.Forms.TextBox textBoxResult;
        private Button btnRefresh;
    }
}
