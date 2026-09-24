namespace ImageOptimizerTool
{
    partial class SettingsForm
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
            this.grpCodec = new System.Windows.Forms.GroupBox();
            this.lblCodecHint = new System.Windows.Forms.Label();
            this.chkJpeg2000 = new System.Windows.Forms.CheckBox();
            this.chkJBig2 = new System.Windows.Forms.CheckBox();
            this.propertyGrid = new System.Windows.Forms.PropertyGrid();
            this.btnOK = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnDefaults = new System.Windows.Forms.Button();
            this.lblFile = new System.Windows.Forms.Label();
            this.grpCodec.SuspendLayout();
            this.SuspendLayout();
            //
            // grpCodec
            //
            this.grpCodec.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.grpCodec.Controls.Add(this.lblCodecHint);
            this.grpCodec.Controls.Add(this.chkJpeg2000);
            this.grpCodec.Controls.Add(this.chkJBig2);
            this.grpCodec.Location = new System.Drawing.Point(12, 12);
            this.grpCodec.Name = "grpCodec";
            this.grpCodec.Size = new System.Drawing.Size(600, 100);
            this.grpCodec.TabIndex = 0;
            this.grpCodec.TabStop = false;
            this.grpCodec.Text = "Codec nén";
            //
            // chkJBig2
            //
            this.chkJBig2.AutoSize = true;
            this.chkJBig2.Location = new System.Drawing.Point(12, 22);
            this.chkJBig2.Name = "chkJBig2";
            this.chkJBig2.Size = new System.Drawing.Size(330, 19);
            this.chkJBig2.TabIndex = 0;
            this.chkJBig2.Text = "Dùng JBIG2 cho trang trắng đen (không tích = CCITT G4)";
            this.chkJBig2.UseVisualStyleBackColor = true;
            //
            // chkJpeg2000
            //
            this.chkJpeg2000.AutoSize = true;
            this.chkJpeg2000.Location = new System.Drawing.Point(12, 46);
            this.chkJpeg2000.Name = "chkJpeg2000";
            this.chkJpeg2000.Size = new System.Drawing.Size(360, 19);
            this.chkJpeg2000.TabIndex = 1;
            this.chkJpeg2000.Text = "Dùng JPEG2000 cho trang màu / xám (không tích = JPEG)";
            this.chkJpeg2000.UseVisualStyleBackColor = true;
            //
            // lblCodecHint
            //
            this.lblCodecHint.AutoSize = true;
            this.lblCodecHint.ForeColor = System.Drawing.SystemColors.GrayText;
            this.lblCodecHint.Location = new System.Drawing.Point(12, 72);
            this.lblCodecHint.Name = "lblCodecHint";
            this.lblCodecHint.Size = new System.Drawing.Size(500, 15);
            this.lblCodecHint.TabIndex = 2;
            this.lblCodecHint.Text = "JBIG2 / JPEG2000 cho file nhỏ hơn nhiều; G4 / JPEG tương thích với mọi phần mềm.";
            //
            // propertyGrid
            //
            this.propertyGrid.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.propertyGrid.Location = new System.Drawing.Point(12, 120);
            this.propertyGrid.Name = "propertyGrid";
            this.propertyGrid.PropertySort = System.Windows.Forms.PropertySort.Categorized;
            this.propertyGrid.Size = new System.Drawing.Size(600, 420);
            this.propertyGrid.TabIndex = 1;
            this.propertyGrid.ToolbarVisible = false;
            //
            // lblFile
            //
            this.lblFile.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.lblFile.AutoEllipsis = true;
            this.lblFile.ForeColor = System.Drawing.SystemColors.GrayText;
            this.lblFile.Location = new System.Drawing.Point(12, 548);
            this.lblFile.Name = "lblFile";
            this.lblFile.Size = new System.Drawing.Size(600, 18);
            this.lblFile.TabIndex = 2;
            //
            // btnDefaults
            //
            this.btnDefaults.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnDefaults.Location = new System.Drawing.Point(12, 572);
            this.btnDefaults.Name = "btnDefaults";
            this.btnDefaults.Size = new System.Drawing.Size(140, 28);
            this.btnDefaults.TabIndex = 3;
            this.btnDefaults.Text = "Khôi phục mặc định";
            this.btnDefaults.UseVisualStyleBackColor = true;
            this.btnDefaults.Click += new System.EventHandler(this.btnDefaults_Click);
            //
            // btnOK
            //
            this.btnOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOK.Location = new System.Drawing.Point(426, 572);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(90, 28);
            this.btnOK.TabIndex = 4;
            this.btnOK.Text = "Lưu";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            //
            // btnCancel
            //
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(522, 572);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(90, 28);
            this.btnCancel.TabIndex = 5;
            this.btnCancel.Text = "Huỷ";
            this.btnCancel.UseVisualStyleBackColor = true;
            //
            // SettingsForm
            //
            this.AcceptButton = this.btnOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(624, 612);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.btnDefaults);
            this.Controls.Add(this.lblFile);
            this.Controls.Add(this.propertyGrid);
            this.Controls.Add(this.grpCodec);
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(520, 450);
            this.Name = "SettingsForm";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Cài đặt";
            this.grpCodec.ResumeLayout(false);
            this.grpCodec.PerformLayout();
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.GroupBox grpCodec;
        private System.Windows.Forms.Label lblCodecHint;
        private System.Windows.Forms.CheckBox chkJpeg2000;
        private System.Windows.Forms.CheckBox chkJBig2;
        private System.Windows.Forms.PropertyGrid propertyGrid;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnDefaults;
        private System.Windows.Forms.Label lblFile;
    }
}
