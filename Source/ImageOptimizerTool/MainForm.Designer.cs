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
            this.components = new System.ComponentModel.Container();
            this.menuStrip = new System.Windows.Forms.MenuStrip();
            this.mnuFile = new System.Windows.Forms.ToolStripMenuItem();
            this.mnuNewProject = new System.Windows.Forms.ToolStripMenuItem();
            this.mnuOpenProject = new System.Windows.Forms.ToolStripMenuItem();
            this.mnuSaveProject = new System.Windows.Forms.ToolStripMenuItem();
            this.mnuSaveProjectAs = new System.Windows.Forms.ToolStripMenuItem();
            this.mnuFileSep1 = new System.Windows.Forms.ToolStripSeparator();
            this.mnuImportImages = new System.Windows.Forms.ToolStripMenuItem();
            this.mnuImportPdf = new System.Windows.Forms.ToolStripMenuItem();
            this.mnuFileSep2 = new System.Windows.Forms.ToolStripSeparator();
            this.mnuExportPdf = new System.Windows.Forms.ToolStripMenuItem();
            this.mnuExportTiff = new System.Windows.Forms.ToolStripMenuItem();
            this.mnuFileSep3 = new System.Windows.Forms.ToolStripSeparator();
            this.mnuExit = new System.Windows.Forms.ToolStripMenuItem();
            this.mnuScanMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.mnuScan = new System.Windows.Forms.ToolStripMenuItem();
            this.mnuCancelScan = new System.Windows.Forms.ToolStripMenuItem();
            this.mnuPage = new System.Windows.Forms.ToolStripMenuItem();
            this.mnuUndo = new System.Windows.Forms.ToolStripMenuItem();
            this.mnuRedo = new System.Windows.Forms.ToolStripMenuItem();
            this.mnuPageSep1 = new System.Windows.Forms.ToolStripSeparator();
            this.mnuRotateLeft = new System.Windows.Forms.ToolStripMenuItem();
            this.mnuRotateRight = new System.Windows.Forms.ToolStripMenuItem();
            this.mnuRotate180 = new System.Windows.Forms.ToolStripMenuItem();
            this.mnuPageSep2 = new System.Windows.Forms.ToolStripSeparator();
            this.mnuAutoProcess = new System.Windows.Forms.ToolStripMenuItem();
            this.mnuInsertPages = new System.Windows.Forms.ToolStripMenuItem();
            this.mnuPageSep3 = new System.Windows.Forms.ToolStripSeparator();
            this.mnuDeletePages = new System.Windows.Forms.ToolStripMenuItem();
            this.mnuClearAll = new System.Windows.Forms.ToolStripMenuItem();
            this.mnuSettingsMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.mnuSettings = new System.Windows.Forms.ToolStripMenuItem();
            this.mnuScanProfiles = new System.Windows.Forms.ToolStripMenuItem();
            this.mnuOpenLogFolder = new System.Windows.Forms.ToolStripMenuItem();
            this.mnuHelp = new System.Windows.Forms.ToolStripMenuItem();
            this.mnuAbout = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStrip = new System.Windows.Forms.ToolStrip();
            this.tsbScan = new System.Windows.Forms.ToolStripButton();
            this.tsbImportImages = new System.Windows.Forms.ToolStripButton();
            this.tsbImportPdf = new System.Windows.Forms.ToolStripButton();
            this.tsSep1 = new System.Windows.Forms.ToolStripSeparator();
            this.tsbRotateLeft = new System.Windows.Forms.ToolStripButton();
            this.tsbRotateRight = new System.Windows.Forms.ToolStripButton();
            this.tsbDelete = new System.Windows.Forms.ToolStripButton();
            this.tsbUndo = new System.Windows.Forms.ToolStripButton();
            this.tsbRedo = new System.Windows.Forms.ToolStripButton();
            this.tsSep2 = new System.Windows.Forms.ToolStripSeparator();
            this.tsbExportPdf = new System.Windows.Forms.ToolStripButton();
            this.tsbExportTiff = new System.Windows.Forms.ToolStripButton();
            this.tsSep3 = new System.Windows.Forms.ToolStripSeparator();
            this.tsbSettings = new System.Windows.Forms.ToolStripButton();
            this.splitContainer = new System.Windows.Forms.SplitContainer();
            this.lvPages = new System.Windows.Forms.ListView();
            this.imlThumbs = new System.Windows.Forms.ImageList(this.components);
            this.picPreview = new System.Windows.Forms.PictureBox();
            this.lblPreviewInfo = new System.Windows.Forms.Label();
            this.statusStrip = new System.Windows.Forms.StatusStrip();
            this.lblStatus = new System.Windows.Forms.ToolStripStatusLabel();
            this.lblPageCount = new System.Windows.Forms.ToolStripStatusLabel();
            this.progressBar = new System.Windows.Forms.ToolStripProgressBar();
            this.lnkCancel = new System.Windows.Forms.ToolStripStatusLabel();
            this.menuStrip.SuspendLayout();
            this.toolStrip.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).BeginInit();
            this.splitContainer.Panel1.SuspendLayout();
            this.splitContainer.Panel2.SuspendLayout();
            this.splitContainer.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.picPreview)).BeginInit();
            this.statusStrip.SuspendLayout();
            this.SuspendLayout();
            //
            // menuStrip
            //
            this.menuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.mnuFile,
            this.mnuScanMenu,
            this.mnuPage,
            this.mnuSettingsMenu,
            this.mnuHelp});
            this.menuStrip.Location = new System.Drawing.Point(0, 0);
            this.menuStrip.Name = "menuStrip";
            this.menuStrip.Size = new System.Drawing.Size(1184, 24);
            this.menuStrip.TabIndex = 0;
            //
            // mnuFile
            //
            this.mnuFile.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.mnuNewProject,
            this.mnuOpenProject,
            this.mnuSaveProject,
            this.mnuSaveProjectAs,
            this.mnuFileSep1,
            this.mnuImportImages,
            this.mnuImportPdf,
            this.mnuFileSep2,
            this.mnuExportPdf,
            this.mnuExportTiff,
            this.mnuFileSep3,
            this.mnuExit});
            this.mnuFile.Name = "mnuFile";
            this.mnuFile.Text = "&Tệp";
            //
            // mnuNewProject
            //
            this.mnuNewProject.Name = "mnuNewProject";
            this.mnuNewProject.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.N)));
            this.mnuNewProject.Text = "Dự án &mới";
            this.mnuNewProject.Click += new System.EventHandler(this.mnuNewProject_Click);
            //
            // mnuOpenProject
            //
            this.mnuOpenProject.Name = "mnuOpenProject";
            this.mnuOpenProject.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.O)));
            this.mnuOpenProject.Text = "&Mở dự án...";
            this.mnuOpenProject.Click += new System.EventHandler(this.mnuOpenProject_Click);
            //
            // mnuSaveProject
            //
            this.mnuSaveProject.Name = "mnuSaveProject";
            this.mnuSaveProject.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.S)));
            this.mnuSaveProject.Text = "&Lưu dự án";
            this.mnuSaveProject.Click += new System.EventHandler(this.mnuSaveProject_Click);
            //
            // mnuSaveProjectAs
            //
            this.mnuSaveProjectAs.Name = "mnuSaveProjectAs";
            this.mnuSaveProjectAs.Text = "Lưu dự án &thành...";
            this.mnuSaveProjectAs.Click += new System.EventHandler(this.mnuSaveProjectAs_Click);
            //
            // mnuImportImages
            //
            this.mnuImportImages.Name = "mnuImportImages";
            this.mnuImportImages.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.I)));
            this.mnuImportImages.Text = "Import &ảnh...";
            this.mnuImportImages.Click += new System.EventHandler(this.mnuImportImages_Click);
            //
            // mnuImportPdf
            //
            this.mnuImportPdf.Name = "mnuImportPdf";
            this.mnuImportPdf.Text = "Import &PDF...";
            this.mnuImportPdf.Click += new System.EventHandler(this.mnuImportPdf_Click);
            //
            // mnuExportPdf
            //
            this.mnuExportPdf.Name = "mnuExportPdf";
            this.mnuExportPdf.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.E)));
            this.mnuExportPdf.Text = "&Xuất PDF...";
            this.mnuExportPdf.Click += new System.EventHandler(this.mnuExportPdf_Click);
            //
            // mnuExportTiff
            //
            this.mnuExportTiff.Name = "mnuExportTiff";
            this.mnuExportTiff.Text = "Xuất &TIFF...";
            this.mnuExportTiff.Click += new System.EventHandler(this.mnuExportTiff_Click);
            //
            // mnuExit
            //
            this.mnuExit.Name = "mnuExit";
            this.mnuExit.Text = "Th&oát";
            this.mnuExit.Click += new System.EventHandler(this.mnuExit_Click);
            //
            // mnuScanMenu
            //
            this.mnuScanMenu.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.mnuScan,
            this.mnuCancelScan});
            this.mnuScanMenu.Name = "mnuScanMenu";
            this.mnuScanMenu.Text = "&Scan";
            //
            // mnuScan
            //
            this.mnuScan.Name = "mnuScan";
            this.mnuScan.ShortcutKeys = System.Windows.Forms.Keys.F5;
            this.mnuScan.Text = "&Scan...";
            this.mnuScan.Click += new System.EventHandler(this.mnuScan_Click);
            //
            // mnuCancelScan
            //
            this.mnuCancelScan.Enabled = false;
            this.mnuCancelScan.Name = "mnuCancelScan";
            this.mnuCancelScan.ShortcutKeys = System.Windows.Forms.Keys.F6;
            this.mnuCancelScan.Text = "&Huỷ scan";
            this.mnuCancelScan.Click += new System.EventHandler(this.mnuCancelScan_Click);
            //
            // mnuPage
            //
            this.mnuPage.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.mnuUndo,
            this.mnuRedo,
            this.mnuPageSep1,
            this.mnuRotateLeft,
            this.mnuRotateRight,
            this.mnuRotate180,
            this.mnuPageSep2,
            this.mnuAutoProcess,
            this.mnuInsertPages,
            this.mnuPageSep3,
            this.mnuDeletePages,
            this.mnuClearAll});
            this.mnuPage.Name = "mnuPage";
            this.mnuPage.Text = "T&rang";
            //
            // mnuUndo
            //
            this.mnuUndo.Name = "mnuUndo";
            this.mnuUndo.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Z)));
            this.mnuUndo.Text = "&Hoàn tác";
            this.mnuUndo.Click += new System.EventHandler(this.mnuUndo_Click);
            //
            // mnuRedo
            //
            this.mnuRedo.Name = "mnuRedo";
            this.mnuRedo.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Y)));
            this.mnuRedo.Text = "&Làm lại";
            this.mnuRedo.Click += new System.EventHandler(this.mnuRedo_Click);
            //
            // mnuRotateLeft
            //
            this.mnuRotateLeft.Name = "mnuRotateLeft";
            this.mnuRotateLeft.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.L)));
            this.mnuRotateLeft.Text = "Xoay &trái 90°";
            this.mnuRotateLeft.Click += new System.EventHandler(this.mnuRotateLeft_Click);
            //
            // mnuRotateRight
            //
            this.mnuRotateRight.Name = "mnuRotateRight";
            this.mnuRotateRight.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.R)));
            this.mnuRotateRight.Text = "Xoay &phải 90°";
            this.mnuRotateRight.Click += new System.EventHandler(this.mnuRotateRight_Click);
            //
            // mnuRotate180
            //
            this.mnuRotate180.Name = "mnuRotate180";
            this.mnuRotate180.Text = "Xoay &180°";
            this.mnuRotate180.Click += new System.EventHandler(this.mnuRotate180_Click);
            //
            // mnuAutoProcess
            //
            this.mnuAutoProcess.Name = "mnuAutoProcess";
            this.mnuAutoProcess.Text = "Tự &xử lý trang đã chọn (nghiêng / viền / chiều)";
            this.mnuAutoProcess.Click += new System.EventHandler(this.mnuAutoProcess_Click);
            //
            // mnuInsertPages
            //
            this.mnuInsertPages.Name = "mnuInsertPages";
            this.mnuInsertPages.Text = "&Chèn trang trước trang đã chọn...";
            this.mnuInsertPages.Click += new System.EventHandler(this.mnuInsertPages_Click);
            //
            // mnuDeletePages
            //
            this.mnuDeletePages.Name = "mnuDeletePages";
            this.mnuDeletePages.ShortcutKeys = System.Windows.Forms.Keys.Delete;
            this.mnuDeletePages.Text = "&Xoá trang đã chọn";
            this.mnuDeletePages.Click += new System.EventHandler(this.mnuDeletePages_Click);
            //
            // mnuClearAll
            //
            this.mnuClearAll.Name = "mnuClearAll";
            this.mnuClearAll.Text = "Xoá &tất cả trang";
            this.mnuClearAll.Click += new System.EventHandler(this.mnuClearAll_Click);
            //
            // mnuSettingsMenu
            //
            this.mnuSettingsMenu.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.mnuSettings,
            this.mnuScanProfiles,
            this.mnuOpenLogFolder});
            this.mnuSettingsMenu.Name = "mnuSettingsMenu";
            this.mnuSettingsMenu.Text = "&Cài đặt";
            //
            // mnuSettings
            //
            this.mnuSettings.Name = "mnuSettings";
            this.mnuSettings.ShortcutKeys = System.Windows.Forms.Keys.F9;
            this.mnuSettings.Text = "&Cài đặt...";
            this.mnuSettings.Click += new System.EventHandler(this.mnuSettings_Click);
            //
            // mnuScanProfiles
            //
            this.mnuScanProfiles.Name = "mnuScanProfiles";
            this.mnuScanProfiles.Text = "&Profile máy scan...";
            this.mnuScanProfiles.Click += new System.EventHandler(this.mnuScanProfiles_Click);
            //
            // mnuOpenLogFolder
            //
            this.mnuOpenLogFolder.Name = "mnuOpenLogFolder";
            this.mnuOpenLogFolder.Text = "Mở thư mục &log";
            this.mnuOpenLogFolder.Click += new System.EventHandler(this.mnuOpenLogFolder_Click);
            //
            // mnuHelp
            //
            this.mnuHelp.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.mnuAbout});
            this.mnuHelp.Name = "mnuHelp";
            this.mnuHelp.Text = "Trợ &giúp";
            //
            // mnuAbout
            //
            this.mnuAbout.Name = "mnuAbout";
            this.mnuAbout.Text = "&Giới thiệu / license...";
            this.mnuAbout.Click += new System.EventHandler(this.mnuAbout_Click);
            //
            // toolStrip
            //
            this.toolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.tsbScan,
            this.tsbImportImages,
            this.tsbImportPdf,
            this.tsSep1,
            this.tsbRotateLeft,
            this.tsbRotateRight,
            this.tsbDelete,
            this.tsbUndo,
            this.tsbRedo,
            this.tsSep2,
            this.tsbExportPdf,
            this.tsbExportTiff,
            this.tsSep3,
            this.tsbSettings});
            this.toolStrip.Location = new System.Drawing.Point(0, 24);
            this.toolStrip.Name = "toolStrip";
            this.toolStrip.Size = new System.Drawing.Size(1184, 25);
            this.toolStrip.TabIndex = 1;
            //
            // tsbScan
            //
            this.tsbScan.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.tsbScan.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.tsbScan.Name = "tsbScan";
            this.tsbScan.Text = "Scan";
            this.tsbScan.Click += new System.EventHandler(this.mnuScan_Click);
            //
            // tsbImportImages
            //
            this.tsbImportImages.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.tsbImportImages.Name = "tsbImportImages";
            this.tsbImportImages.Text = "Import ảnh";
            this.tsbImportImages.Click += new System.EventHandler(this.mnuImportImages_Click);
            //
            // tsbImportPdf
            //
            this.tsbImportPdf.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.tsbImportPdf.Name = "tsbImportPdf";
            this.tsbImportPdf.Text = "Import PDF";
            this.tsbImportPdf.Click += new System.EventHandler(this.mnuImportPdf_Click);
            //
            // tsbRotateLeft
            //
            this.tsbRotateLeft.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.tsbRotateLeft.Name = "tsbRotateLeft";
            this.tsbRotateLeft.Text = "⟲ Xoay trái";
            this.tsbRotateLeft.Click += new System.EventHandler(this.mnuRotateLeft_Click);
            //
            // tsbRotateRight
            //
            this.tsbRotateRight.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.tsbRotateRight.Name = "tsbRotateRight";
            this.tsbRotateRight.Text = "⟳ Xoay phải";
            this.tsbRotateRight.Click += new System.EventHandler(this.mnuRotateRight_Click);
            //
            // tsbDelete
            //
            this.tsbDelete.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.tsbDelete.Name = "tsbDelete";
            this.tsbDelete.Text = "Xoá";
            this.tsbDelete.Click += new System.EventHandler(this.mnuDeletePages_Click);
            //
            // tsbUndo
            //
            this.tsbUndo.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.tsbUndo.Name = "tsbUndo";
            this.tsbUndo.Text = "↶ Hoàn tác";
            this.tsbUndo.Click += new System.EventHandler(this.mnuUndo_Click);
            //
            // tsbRedo
            //
            this.tsbRedo.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.tsbRedo.Name = "tsbRedo";
            this.tsbRedo.Text = "↷ Làm lại";
            this.tsbRedo.Click += new System.EventHandler(this.mnuRedo_Click);
            //
            // tsbExportPdf
            //
            this.tsbExportPdf.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.tsbExportPdf.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.tsbExportPdf.Name = "tsbExportPdf";
            this.tsbExportPdf.Text = "Xuất PDF";
            this.tsbExportPdf.Click += new System.EventHandler(this.mnuExportPdf_Click);
            //
            // tsbExportTiff
            //
            this.tsbExportTiff.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.tsbExportTiff.Name = "tsbExportTiff";
            this.tsbExportTiff.Text = "Xuất TIFF";
            this.tsbExportTiff.Click += new System.EventHandler(this.mnuExportTiff_Click);
            //
            // tsbSettings
            //
            this.tsbSettings.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.tsbSettings.Name = "tsbSettings";
            this.tsbSettings.Text = "Cài đặt";
            this.tsbSettings.Click += new System.EventHandler(this.mnuSettings_Click);
            //
            // splitContainer
            //
            this.splitContainer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer.Location = new System.Drawing.Point(0, 49);
            this.splitContainer.Name = "splitContainer";
            this.splitContainer.Panel1.Controls.Add(this.lvPages);
            this.splitContainer.Panel2.Controls.Add(this.picPreview);
            this.splitContainer.Panel2.Controls.Add(this.lblPreviewInfo);
            this.splitContainer.Size = new System.Drawing.Size(1184, 590);
            this.splitContainer.SplitterDistance = 380;
            this.splitContainer.TabIndex = 2;
            //
            // lvPages
            //
            this.lvPages.AllowDrop = true;
            this.lvPages.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lvPages.HideSelection = false;
            this.lvPages.LargeImageList = this.imlThumbs;
            this.lvPages.Location = new System.Drawing.Point(0, 0);
            this.lvPages.Name = "lvPages";
            this.lvPages.Size = new System.Drawing.Size(380, 590);
            this.lvPages.TabIndex = 0;
            this.lvPages.UseCompatibleStateImageBehavior = false;
            this.lvPages.ItemDrag += new System.Windows.Forms.ItemDragEventHandler(this.lvPages_ItemDrag);
            this.lvPages.SelectedIndexChanged += new System.EventHandler(this.lvPages_SelectedIndexChanged);
            this.lvPages.DragDrop += new System.Windows.Forms.DragEventHandler(this.lvPages_DragDrop);
            this.lvPages.DragEnter += new System.Windows.Forms.DragEventHandler(this.lvPages_DragOver);
            this.lvPages.DragOver += new System.Windows.Forms.DragEventHandler(this.lvPages_DragOver);
            this.lvPages.DragLeave += new System.EventHandler(this.lvPages_DragLeave);
            //
            // imlThumbs
            //
            this.imlThumbs.ColorDepth = System.Windows.Forms.ColorDepth.Depth24Bit;
            this.imlThumbs.ImageSize = new System.Drawing.Size(120, 160);
            this.imlThumbs.TransparentColor = System.Drawing.Color.Transparent;
            //
            // picPreview
            //
            this.picPreview.BackColor = System.Drawing.Color.DimGray;
            this.picPreview.Dock = System.Windows.Forms.DockStyle.Fill;
            this.picPreview.Location = new System.Drawing.Point(0, 23);
            this.picPreview.Name = "picPreview";
            this.picPreview.Size = new System.Drawing.Size(800, 567);
            this.picPreview.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.picPreview.TabIndex = 1;
            this.picPreview.TabStop = false;
            //
            // lblPreviewInfo
            //
            this.lblPreviewInfo.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblPreviewInfo.Location = new System.Drawing.Point(0, 0);
            this.lblPreviewInfo.Name = "lblPreviewInfo";
            this.lblPreviewInfo.Padding = new System.Windows.Forms.Padding(4, 4, 0, 0);
            this.lblPreviewInfo.Size = new System.Drawing.Size(800, 23);
            this.lblPreviewInfo.TabIndex = 0;
            //
            // statusStrip
            //
            this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.lblStatus,
            this.lblPageCount,
            this.progressBar,
            this.lnkCancel});
            this.statusStrip.Location = new System.Drawing.Point(0, 639);
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.Size = new System.Drawing.Size(1184, 22);
            this.statusStrip.TabIndex = 3;
            //
            // lblStatus
            //
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Spring = true;
            this.lblStatus.Text = "Sẵn sàng";
            this.lblStatus.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // lblPageCount
            //
            this.lblPageCount.Name = "lblPageCount";
            this.lblPageCount.Text = "0 trang";
            //
            // progressBar
            //
            this.progressBar.Name = "progressBar";
            this.progressBar.Size = new System.Drawing.Size(200, 16);
            this.progressBar.Visible = false;
            //
            // lnkCancel
            //
            this.lnkCancel.IsLink = true;
            this.lnkCancel.Name = "lnkCancel";
            this.lnkCancel.Text = "Huỷ";
            this.lnkCancel.Visible = false;
            this.lnkCancel.Click += new System.EventHandler(this.lnkCancel_Click);
            //
            // MainForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1184, 661);
            this.Controls.Add(this.splitContainer);
            this.Controls.Add(this.statusStrip);
            this.Controls.Add(this.toolStrip);
            this.Controls.Add(this.menuStrip);
            this.MainMenuStrip = this.menuStrip;
            this.MinimumSize = new System.Drawing.Size(800, 500);
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Image Optimizer Tool";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.menuStrip.ResumeLayout(false);
            this.menuStrip.PerformLayout();
            this.toolStrip.ResumeLayout(false);
            this.toolStrip.PerformLayout();
            this.splitContainer.Panel1.ResumeLayout(false);
            this.splitContainer.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).EndInit();
            this.splitContainer.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.picPreview)).EndInit();
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.MenuStrip menuStrip;
        private System.Windows.Forms.ToolStripMenuItem mnuFile;
        private System.Windows.Forms.ToolStripMenuItem mnuNewProject;
        private System.Windows.Forms.ToolStripMenuItem mnuOpenProject;
        private System.Windows.Forms.ToolStripMenuItem mnuSaveProject;
        private System.Windows.Forms.ToolStripMenuItem mnuSaveProjectAs;
        private System.Windows.Forms.ToolStripSeparator mnuFileSep1;
        private System.Windows.Forms.ToolStripMenuItem mnuImportImages;
        private System.Windows.Forms.ToolStripMenuItem mnuImportPdf;
        private System.Windows.Forms.ToolStripSeparator mnuFileSep2;
        private System.Windows.Forms.ToolStripMenuItem mnuExportPdf;
        private System.Windows.Forms.ToolStripMenuItem mnuExportTiff;
        private System.Windows.Forms.ToolStripSeparator mnuFileSep3;
        private System.Windows.Forms.ToolStripMenuItem mnuExit;
        private System.Windows.Forms.ToolStripMenuItem mnuScanMenu;
        private System.Windows.Forms.ToolStripMenuItem mnuScan;
        private System.Windows.Forms.ToolStripMenuItem mnuCancelScan;
        private System.Windows.Forms.ToolStripMenuItem mnuPage;
        private System.Windows.Forms.ToolStripMenuItem mnuUndo;
        private System.Windows.Forms.ToolStripMenuItem mnuRedo;
        private System.Windows.Forms.ToolStripSeparator mnuPageSep1;
        private System.Windows.Forms.ToolStripMenuItem mnuRotateLeft;
        private System.Windows.Forms.ToolStripMenuItem mnuRotateRight;
        private System.Windows.Forms.ToolStripMenuItem mnuRotate180;
        private System.Windows.Forms.ToolStripSeparator mnuPageSep2;
        private System.Windows.Forms.ToolStripMenuItem mnuAutoProcess;
        private System.Windows.Forms.ToolStripMenuItem mnuInsertPages;
        private System.Windows.Forms.ToolStripSeparator mnuPageSep3;
        private System.Windows.Forms.ToolStripMenuItem mnuDeletePages;
        private System.Windows.Forms.ToolStripMenuItem mnuClearAll;
        private System.Windows.Forms.ToolStripMenuItem mnuSettingsMenu;
        private System.Windows.Forms.ToolStripMenuItem mnuSettings;
        private System.Windows.Forms.ToolStripMenuItem mnuScanProfiles;
        private System.Windows.Forms.ToolStripMenuItem mnuOpenLogFolder;
        private System.Windows.Forms.ToolStripMenuItem mnuHelp;
        private System.Windows.Forms.ToolStripMenuItem mnuAbout;
        private System.Windows.Forms.ToolStrip toolStrip;
        private System.Windows.Forms.ToolStripButton tsbScan;
        private System.Windows.Forms.ToolStripButton tsbImportImages;
        private System.Windows.Forms.ToolStripButton tsbImportPdf;
        private System.Windows.Forms.ToolStripSeparator tsSep1;
        private System.Windows.Forms.ToolStripButton tsbRotateLeft;
        private System.Windows.Forms.ToolStripButton tsbRotateRight;
        private System.Windows.Forms.ToolStripButton tsbDelete;
        private System.Windows.Forms.ToolStripButton tsbUndo;
        private System.Windows.Forms.ToolStripButton tsbRedo;
        private System.Windows.Forms.ToolStripSeparator tsSep2;
        private System.Windows.Forms.ToolStripButton tsbExportPdf;
        private System.Windows.Forms.ToolStripButton tsbExportTiff;
        private System.Windows.Forms.ToolStripSeparator tsSep3;
        private System.Windows.Forms.ToolStripButton tsbSettings;
        private System.Windows.Forms.SplitContainer splitContainer;
        private System.Windows.Forms.ListView lvPages;
        private System.Windows.Forms.ImageList imlThumbs;
        private System.Windows.Forms.PictureBox picPreview;
        private System.Windows.Forms.Label lblPreviewInfo;
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel lblStatus;
        private System.Windows.Forms.ToolStripStatusLabel lblPageCount;
        private System.Windows.Forms.ToolStripProgressBar progressBar;
        private System.Windows.Forms.ToolStripStatusLabel lnkCancel;
    }
}
