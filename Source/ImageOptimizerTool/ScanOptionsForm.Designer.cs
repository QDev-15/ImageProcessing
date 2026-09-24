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

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.lblSource = new System.Windows.Forms.Label();
            this.cboSource = new System.Windows.Forms.ComboBox();
            this.lblProfile = new System.Windows.Forms.Label();
            this.cboProfile = new System.Windows.Forms.ComboBox();
            this.btnNewProfile = new System.Windows.Forms.Button();
            this.btnDeleteProfile = new System.Windows.Forms.Button();
            this.propertyGrid = new System.Windows.Forms.PropertyGrid();
            this.btnScan = new System.Windows.Forms.Button();
            this.btnClose = new System.Windows.Forms.Button();
            this.SuspendLayout();
            //
            // lblSource
            //
            this.lblSource.AutoSize = true;
            this.lblSource.Location = new System.Drawing.Point(12, 16);
            this.lblSource.Name = "lblSource";
            this.lblSource.Size = new System.Drawing.Size(60, 15);
            this.lblSource.TabIndex = 0;
            this.lblSource.Text = "Máy scan:";
            //
            // cboSource
            //
            this.cboSource.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.cboSource.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboSource.Location = new System.Drawing.Point(90, 12);
            this.cboSource.Name = "cboSource";
            this.cboSource.Size = new System.Drawing.Size(418, 23);
            this.cboSource.TabIndex = 1;
            //
            // lblProfile
            //
            this.lblProfile.AutoSize = true;
            this.lblProfile.Location = new System.Drawing.Point(12, 50);
            this.lblProfile.Name = "lblProfile";
            this.lblProfile.Size = new System.Drawing.Size(44, 15);
            this.lblProfile.TabIndex = 2;
            this.lblProfile.Text = "Profile:";
            //
            // cboProfile
            //
            this.cboProfile.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.cboProfile.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboProfile.Location = new System.Drawing.Point(90, 46);
            this.cboProfile.Name = "cboProfile";
            this.cboProfile.Size = new System.Drawing.Size(250, 23);
            this.cboProfile.TabIndex = 3;
            this.cboProfile.SelectedIndexChanged += new System.EventHandler(this.cboProfile_SelectedIndexChanged);
            //
            // btnNewProfile
            //
            this.btnNewProfile.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnNewProfile.Location = new System.Drawing.Point(346, 45);
            this.btnNewProfile.Name = "btnNewProfile";
            this.btnNewProfile.Size = new System.Drawing.Size(80, 25);
            this.btnNewProfile.TabIndex = 4;
            this.btnNewProfile.Text = "Thêm...";
            this.btnNewProfile.UseVisualStyleBackColor = true;
            this.btnNewProfile.Click += new System.EventHandler(this.btnNewProfile_Click);
            //
            // btnDeleteProfile
            //
            this.btnDeleteProfile.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnDeleteProfile.Location = new System.Drawing.Point(428, 45);
            this.btnDeleteProfile.Name = "btnDeleteProfile";
            this.btnDeleteProfile.Size = new System.Drawing.Size(80, 25);
            this.btnDeleteProfile.TabIndex = 5;
            this.btnDeleteProfile.Text = "Xoá";
            this.btnDeleteProfile.UseVisualStyleBackColor = true;
            this.btnDeleteProfile.Click += new System.EventHandler(this.btnDeleteProfile_Click);
            //
            // propertyGrid
            //
            this.propertyGrid.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.propertyGrid.HelpVisible = true;
            this.propertyGrid.Location = new System.Drawing.Point(12, 80);
            this.propertyGrid.Name = "propertyGrid";
            this.propertyGrid.PropertySort = System.Windows.Forms.PropertySort.NoSort;
            this.propertyGrid.Size = new System.Drawing.Size(496, 260);
            this.propertyGrid.TabIndex = 6;
            this.propertyGrid.ToolbarVisible = false;
            this.propertyGrid.PropertyValueChanged += new System.Windows.Forms.PropertyValueChangedEventHandler(this.propertyGrid_PropertyValueChanged);
            //
            // btnScan
            //
            this.btnScan.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnScan.Location = new System.Drawing.Point(322, 350);
            this.btnScan.Name = "btnScan";
            this.btnScan.Size = new System.Drawing.Size(90, 28);
            this.btnScan.TabIndex = 7;
            this.btnScan.Text = "Scan";
            this.btnScan.UseVisualStyleBackColor = true;
            this.btnScan.Click += new System.EventHandler(this.btnScan_Click);
            //
            // btnClose
            //
            this.btnClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnClose.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnClose.Location = new System.Drawing.Point(418, 350);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(90, 28);
            this.btnClose.TabIndex = 8;
            this.btnClose.Text = "Đóng";
            this.btnClose.UseVisualStyleBackColor = true;
            //
            // ScanOptionsForm
            //
            this.AcceptButton = this.btnScan;
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnClose;
            this.ClientSize = new System.Drawing.Size(520, 390);
            this.Controls.Add(this.btnClose);
            this.Controls.Add(this.btnScan);
            this.Controls.Add(this.propertyGrid);
            this.Controls.Add(this.btnDeleteProfile);
            this.Controls.Add(this.btnNewProfile);
            this.Controls.Add(this.cboProfile);
            this.Controls.Add(this.lblProfile);
            this.Controls.Add(this.cboSource);
            this.Controls.Add(this.lblSource);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(460, 360);
            this.Name = "ScanOptionsForm";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Scan";
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Label lblSource;
        private System.Windows.Forms.ComboBox cboSource;
        private System.Windows.Forms.Label lblProfile;
        private System.Windows.Forms.ComboBox cboProfile;
        private System.Windows.Forms.Button btnNewProfile;
        private System.Windows.Forms.Button btnDeleteProfile;
        private System.Windows.Forms.PropertyGrid propertyGrid;
        private System.Windows.Forms.Button btnScan;
        private System.Windows.Forms.Button btnClose;
    }
}
