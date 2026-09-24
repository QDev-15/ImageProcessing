namespace ImageOptimizerTool
{
    partial class ScanOptionsForm
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

        private void InitializeComponent()
        {
            this.lblSource = new System.Windows.Forms.Label();
            this.cboSource = new System.Windows.Forms.ComboBox();
            this.lblDpi = new System.Windows.Forms.Label();
            this.cboDpi = new System.Windows.Forms.ComboBox();
            this.lblColorMode = new System.Windows.Forms.Label();
            this.cboColorMode = new System.Windows.Forms.ComboBox();
            this.chkShowDriverUi = new System.Windows.Forms.CheckBox();
            this.chkDuplex = new System.Windows.Forms.CheckBox();
            this.btnScan = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.SuspendLayout();
            //
            // lblSource
            //
            this.lblSource.AutoSize = true;
            this.lblSource.Location = new System.Drawing.Point(12, 15);
            this.lblSource.Name = "lblSource";
            this.lblSource.Size = new System.Drawing.Size(45, 15);
            this.lblSource.TabIndex = 0;
            this.lblSource.Text = "Source:";
            //
            // cboSource
            //
            this.cboSource.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboSource.FormattingEnabled = true;
            this.cboSource.Location = new System.Drawing.Point(112, 12);
            this.cboSource.Name = "cboSource";
            this.cboSource.Size = new System.Drawing.Size(300, 23);
            this.cboSource.TabIndex = 1;
            //
            // lblDpi
            //
            this.lblDpi.AutoSize = true;
            this.lblDpi.Location = new System.Drawing.Point(12, 48);
            this.lblDpi.Name = "lblDpi";
            this.lblDpi.Size = new System.Drawing.Size(31, 15);
            this.lblDpi.TabIndex = 2;
            this.lblDpi.Text = "DPI:";
            //
            // cboDpi
            //
            this.cboDpi.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboDpi.FormattingEnabled = true;
            this.cboDpi.Items.AddRange(new object[] {
            "100",
            "150",
            "200",
            "300",
            "400",
            "600"});
            this.cboDpi.Location = new System.Drawing.Point(112, 45);
            this.cboDpi.Name = "cboDpi";
            this.cboDpi.Size = new System.Drawing.Size(120, 23);
            this.cboDpi.TabIndex = 3;
            //
            // lblColorMode
            //
            this.lblColorMode.AutoSize = true;
            this.lblColorMode.Location = new System.Drawing.Point(12, 81);
            this.lblColorMode.Name = "lblColorMode";
            this.lblColorMode.Size = new System.Drawing.Size(69, 15);
            this.lblColorMode.TabIndex = 4;
            this.lblColorMode.Text = "Color mode:";
            //
            // cboColorMode
            //
            this.cboColorMode.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboColorMode.FormattingEnabled = true;
            this.cboColorMode.Items.AddRange(new object[] {
            "Black & White",
            "Gray",
            "Color"});
            this.cboColorMode.Location = new System.Drawing.Point(112, 78);
            this.cboColorMode.Name = "cboColorMode";
            this.cboColorMode.Size = new System.Drawing.Size(150, 23);
            this.cboColorMode.TabIndex = 5;
            //
            // chkShowDriverUi
            //
            this.chkShowDriverUi.AutoSize = true;
            this.chkShowDriverUi.Location = new System.Drawing.Point(112, 111);
            this.chkShowDriverUi.Name = "chkShowDriverUi";
            this.chkShowDriverUi.Size = new System.Drawing.Size(150, 19);
            this.chkShowDriverUi.TabIndex = 6;
            this.chkShowDriverUi.Text = "Show scanner UI";
            this.chkShowDriverUi.UseVisualStyleBackColor = true;
            //
            // chkDuplex
            //
            this.chkDuplex.AutoSize = true;
            this.chkDuplex.Location = new System.Drawing.Point(112, 136);
            this.chkDuplex.Name = "chkDuplex";
            this.chkDuplex.Size = new System.Drawing.Size(150, 19);
            this.chkDuplex.TabIndex = 9;
            this.chkDuplex.Text = "Duplex (2-sided)";
            this.chkDuplex.UseVisualStyleBackColor = true;
            //
            // btnScan
            //
            this.btnScan.Location = new System.Drawing.Point(256, 176);
            this.btnScan.Name = "btnScan";
            this.btnScan.Size = new System.Drawing.Size(75, 27);
            this.btnScan.TabIndex = 7;
            this.btnScan.Text = "Scan";
            this.btnScan.UseVisualStyleBackColor = true;
            this.btnScan.Click += new System.EventHandler(this.btnScan_Click);
            //
            // btnCancel
            //
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(337, 176);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 27);
            this.btnCancel.TabIndex = 8;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            //
            // ScanOptionsForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(424, 215);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnScan);
            this.Controls.Add(this.chkDuplex);
            this.Controls.Add(this.chkShowDriverUi);
            this.Controls.Add(this.cboColorMode);
            this.Controls.Add(this.lblColorMode);
            this.Controls.Add(this.cboDpi);
            this.Controls.Add(this.lblDpi);
            this.Controls.Add(this.cboSource);
            this.Controls.Add(this.lblSource);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ScanOptionsForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Scan";
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private System.Windows.Forms.Label lblSource;
        private System.Windows.Forms.ComboBox cboSource;
        private System.Windows.Forms.Label lblDpi;
        private System.Windows.Forms.ComboBox cboDpi;
        private System.Windows.Forms.Label lblColorMode;
        private System.Windows.Forms.ComboBox cboColorMode;
        private System.Windows.Forms.CheckBox chkShowDriverUi;
        private System.Windows.Forms.CheckBox chkDuplex;
        private System.Windows.Forms.Button btnScan;
        private System.Windows.Forms.Button btnCancel;
    }
}
