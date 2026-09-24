namespace ImageOptimizerTool
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
            this.lblTempFolder = new System.Windows.Forms.Label();
            this.txtTempFolder = new System.Windows.Forms.TextBox();
            this.btnBrowseTempFolder = new System.Windows.Forms.Button();
            this.lstPages = new System.Windows.Forms.ListBox();
            this.picPreview = new System.Windows.Forms.PictureBox();
            this.btnImportImages = new System.Windows.Forms.Button();
            this.btnImportPdf = new System.Windows.Forms.Button();
            this.btnScan = new System.Windows.Forms.Button();
            this.btnRemoveSelected = new System.Windows.Forms.Button();
            this.btnClearAll = new System.Windows.Forms.Button();
            this.lblPageCount = new System.Windows.Forms.Label();
            this.lblExportType = new System.Windows.Forms.Label();
            this.cboExportType = new System.Windows.Forms.ComboBox();
            this.lblCodec = new System.Windows.Forms.Label();
            this.cboCodec = new System.Windows.Forms.ComboBox();
            this.btnExportPdf = new System.Windows.Forms.Button();
            this.btnExportTiff = new System.Windows.Forms.Button();
            this.lblStatus = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.picPreview)).BeginInit();
            this.SuspendLayout();
            //
            // lblTempFolder
            //
            this.lblTempFolder.AutoSize = true;
            this.lblTempFolder.Location = new System.Drawing.Point(12, 15);
            this.lblTempFolder.Name = "lblTempFolder";
            this.lblTempFolder.Size = new System.Drawing.Size(75, 15);
            this.lblTempFolder.TabIndex = 0;
            this.lblTempFolder.Text = "Temp folder:";
            //
            // txtTempFolder
            //
            this.txtTempFolder.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.txtTempFolder.Location = new System.Drawing.Point(108, 12);
            this.txtTempFolder.Name = "txtTempFolder";
            this.txtTempFolder.Size = new System.Drawing.Size(660, 23);
            this.txtTempFolder.TabIndex = 1;
            this.txtTempFolder.Leave += new System.EventHandler(this.txtTempFolder_Leave);
            //
            // btnBrowseTempFolder
            //
            this.btnBrowseTempFolder.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnBrowseTempFolder.Location = new System.Drawing.Point(776, 11);
            this.btnBrowseTempFolder.Name = "btnBrowseTempFolder";
            this.btnBrowseTempFolder.Size = new System.Drawing.Size(100, 25);
            this.btnBrowseTempFolder.TabIndex = 2;
            this.btnBrowseTempFolder.Text = "Browse...";
            this.btnBrowseTempFolder.UseVisualStyleBackColor = true;
            this.btnBrowseTempFolder.Click += new System.EventHandler(this.btnBrowseTempFolder_Click);
            //
            // lstPages
            //
            this.lstPages.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) | System.Windows.Forms.AnchorStyles.Left)));
            this.lstPages.FormattingEnabled = true;
            this.lstPages.ItemHeight = 15;
            this.lstPages.Location = new System.Drawing.Point(12, 50);
            this.lstPages.Name = "lstPages";
            this.lstPages.Size = new System.Drawing.Size(280, 469);
            this.lstPages.TabIndex = 3;
            this.lstPages.SelectedIndexChanged += new System.EventHandler(this.lstPages_SelectedIndexChanged);
            //
            // picPreview
            //
            this.picPreview.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.picPreview.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.picPreview.Location = new System.Drawing.Point(300, 50);
            this.picPreview.Name = "picPreview";
            this.picPreview.Size = new System.Drawing.Size(456, 469);
            this.picPreview.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.picPreview.TabIndex = 4;
            this.picPreview.TabStop = false;
            //
            // btnImportImages
            //
            this.btnImportImages.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnImportImages.Location = new System.Drawing.Point(764, 50);
            this.btnImportImages.Name = "btnImportImages";
            this.btnImportImages.Size = new System.Drawing.Size(112, 30);
            this.btnImportImages.TabIndex = 5;
            this.btnImportImages.Text = "Import Images...";
            this.btnImportImages.UseVisualStyleBackColor = true;
            this.btnImportImages.Click += new System.EventHandler(this.btnImportImages_Click);
            //
            // btnImportPdf
            //
            this.btnImportPdf.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnImportPdf.Location = new System.Drawing.Point(764, 86);
            this.btnImportPdf.Name = "btnImportPdf";
            this.btnImportPdf.Size = new System.Drawing.Size(112, 30);
            this.btnImportPdf.TabIndex = 6;
            this.btnImportPdf.Text = "Import PDF...";
            this.btnImportPdf.UseVisualStyleBackColor = true;
            this.btnImportPdf.Click += new System.EventHandler(this.btnImportPdf_Click);
            //
            // btnScan
            //
            this.btnScan.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnScan.Location = new System.Drawing.Point(764, 122);
            this.btnScan.Name = "btnScan";
            this.btnScan.Size = new System.Drawing.Size(112, 30);
            this.btnScan.TabIndex = 17;
            this.btnScan.Text = "Scan...";
            this.btnScan.UseVisualStyleBackColor = true;
            this.btnScan.Click += new System.EventHandler(this.btnScan_Click);
            //
            // btnRemoveSelected
            //
            this.btnRemoveSelected.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnRemoveSelected.Location = new System.Drawing.Point(764, 158);
            this.btnRemoveSelected.Name = "btnRemoveSelected";
            this.btnRemoveSelected.Size = new System.Drawing.Size(112, 30);
            this.btnRemoveSelected.TabIndex = 7;
            this.btnRemoveSelected.Text = "Remove Selected";
            this.btnRemoveSelected.UseVisualStyleBackColor = true;
            this.btnRemoveSelected.Click += new System.EventHandler(this.btnRemoveSelected_Click);
            //
            // btnClearAll
            //
            this.btnClearAll.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnClearAll.Location = new System.Drawing.Point(764, 194);
            this.btnClearAll.Name = "btnClearAll";
            this.btnClearAll.Size = new System.Drawing.Size(112, 30);
            this.btnClearAll.TabIndex = 8;
            this.btnClearAll.Text = "Clear All";
            this.btnClearAll.UseVisualStyleBackColor = true;
            this.btnClearAll.Click += new System.EventHandler(this.btnClearAll_Click);
            //
            // lblPageCount
            //
            this.lblPageCount.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.lblPageCount.AutoSize = true;
            this.lblPageCount.Location = new System.Drawing.Point(764, 232);
            this.lblPageCount.Name = "lblPageCount";
            this.lblPageCount.Size = new System.Drawing.Size(53, 15);
            this.lblPageCount.TabIndex = 9;
            this.lblPageCount.Text = "0 page(s)";
            //
            // lblExportType
            //
            this.lblExportType.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.lblExportType.AutoSize = true;
            this.lblExportType.Location = new System.Drawing.Point(12, 529);
            this.lblExportType.Name = "lblExportType";
            this.lblExportType.Size = new System.Drawing.Size(72, 15);
            this.lblExportType.TabIndex = 10;
            this.lblExportType.Text = "Export type:";
            //
            // cboExportType
            //
            this.cboExportType.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.cboExportType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboExportType.FormattingEnabled = true;
            this.cboExportType.Items.AddRange(new object[] {
            "B&W",
            "Color"});
            this.cboExportType.Location = new System.Drawing.Point(92, 525);
            this.cboExportType.Name = "cboExportType";
            this.cboExportType.Size = new System.Drawing.Size(120, 23);
            this.cboExportType.TabIndex = 11;
            this.cboExportType.SelectedIndexChanged += new System.EventHandler(this.cboExportType_SelectedIndexChanged);
            //
            // lblCodec
            //
            this.lblCodec.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.lblCodec.AutoSize = true;
            this.lblCodec.Location = new System.Drawing.Point(220, 529);
            this.lblCodec.Name = "lblCodec";
            this.lblCodec.Size = new System.Drawing.Size(42, 15);
            this.lblCodec.TabIndex = 15;
            this.lblCodec.Text = "Codec:";
            //
            // cboCodec
            //
            this.cboCodec.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.cboCodec.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboCodec.FormattingEnabled = true;
            this.cboCodec.Location = new System.Drawing.Point(268, 525);
            this.cboCodec.Name = "cboCodec";
            this.cboCodec.Size = new System.Drawing.Size(170, 23);
            this.cboCodec.TabIndex = 16;
            //
            // btnExportPdf
            //
            this.btnExportPdf.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnExportPdf.Location = new System.Drawing.Point(448, 524);
            this.btnExportPdf.Name = "btnExportPdf";
            this.btnExportPdf.Size = new System.Drawing.Size(110, 28);
            this.btnExportPdf.TabIndex = 12;
            this.btnExportPdf.Text = "Export PDF...";
            this.btnExportPdf.UseVisualStyleBackColor = true;
            this.btnExportPdf.Click += new System.EventHandler(this.btnExportPdf_Click);
            //
            // btnExportTiff
            //
            this.btnExportTiff.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnExportTiff.Location = new System.Drawing.Point(568, 524);
            this.btnExportTiff.Name = "btnExportTiff";
            this.btnExportTiff.Size = new System.Drawing.Size(110, 28);
            this.btnExportTiff.TabIndex = 13;
            this.btnExportTiff.Text = "Export TIFF...";
            this.btnExportTiff.UseVisualStyleBackColor = true;
            this.btnExportTiff.Click += new System.EventHandler(this.btnExportTiff_Click);
            //
            // lblStatus
            //
            this.lblStatus.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lblStatus.Location = new System.Drawing.Point(12, 561);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(864, 40);
            this.lblStatus.TabIndex = 14;
            this.lblStatus.Text = "Ready.";
            //
            // MainForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(888, 613);
            this.Controls.Add(this.lblStatus);
            this.Controls.Add(this.btnExportTiff);
            this.Controls.Add(this.btnExportPdf);
            this.Controls.Add(this.cboCodec);
            this.Controls.Add(this.lblCodec);
            this.Controls.Add(this.cboExportType);
            this.Controls.Add(this.lblExportType);
            this.Controls.Add(this.lblPageCount);
            this.Controls.Add(this.btnClearAll);
            this.Controls.Add(this.btnRemoveSelected);
            this.Controls.Add(this.btnScan);
            this.Controls.Add(this.btnImportPdf);
            this.Controls.Add(this.btnImportImages);
            this.Controls.Add(this.picPreview);
            this.Controls.Add(this.lstPages);
            this.Controls.Add(this.btnBrowseTempFolder);
            this.Controls.Add(this.txtTempFolder);
            this.Controls.Add(this.lblTempFolder);
            this.MinimumSize = new System.Drawing.Size(700, 400);
            this.Name = "MainForm";
            this.Text = "Image Optimizer Tool - Visual Test";
            this.Load += new System.EventHandler(this.MainForm_Load);
            ((System.ComponentModel.ISupportInitialize)(this.picPreview)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lblTempFolder;
        private System.Windows.Forms.TextBox txtTempFolder;
        private System.Windows.Forms.Button btnBrowseTempFolder;
        private System.Windows.Forms.ListBox lstPages;
        private System.Windows.Forms.PictureBox picPreview;
        private System.Windows.Forms.Button btnImportImages;
        private System.Windows.Forms.Button btnImportPdf;
        private System.Windows.Forms.Button btnScan;
        private System.Windows.Forms.Button btnRemoveSelected;
        private System.Windows.Forms.Button btnClearAll;
        private System.Windows.Forms.Label lblPageCount;
        private System.Windows.Forms.Label lblExportType;
        private System.Windows.Forms.ComboBox cboExportType;
        private System.Windows.Forms.Label lblCodec;
        private System.Windows.Forms.ComboBox cboCodec;
        private System.Windows.Forms.Button btnExportPdf;
        private System.Windows.Forms.Button btnExportTiff;
        private System.Windows.Forms.Label lblStatus;
    }
}
