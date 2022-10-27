namespace WidgetSample
{
	partial class Form1
	{
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing)
		{
			if (disposing && (components != null))
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Windows Form Designer generated code

		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
            this.gadgetGrid = new System.Windows.Forms.DataGridView();
            this.bAddGadget = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.bAddWidget = new System.Windows.Forms.Button();
            this.widgetGrid = new System.Windows.Forms.DataGridView();
            this.label2 = new System.Windows.Forms.Label();
            this.bInfo = new System.Windows.Forms.Button();
            this.bHorrificException = new System.Windows.Forms.Button();
            this.bRemoveGadget = new System.Windows.Forms.Button();
            this.bRemoveWidget = new System.Windows.Forms.Button();
            this.btnTraceScope = new System.Windows.Forms.Button();
            this.btn100 = new System.Windows.Forms.Button();
            this.btn1000 = new System.Windows.Forms.Button();
            this.btn10 = new System.Windows.Forms.Button();
            this.chkSystem = new System.Windows.Forms.CheckBox();
            this.chkWidgets = new System.Windows.Forms.CheckBox();
            this.chkGadgets = new System.Windows.Forms.CheckBox();
            this.txtContent = new System.Windows.Forms.TextBox();
            this.btnStartHosting = new System.Windows.Forms.Button();
            this.btnStopHosting = new System.Windows.Forms.Button();
            this.bNotice = new System.Windows.Forms.Button();
            this.bWarn = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.gadgetGrid)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.widgetGrid)).BeginInit();
            this.SuspendLayout();
            // 
            // gadgetGrid
            // 
            this.gadgetGrid.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.gadgetGrid.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gadgetGrid.Location = new System.Drawing.Point(1, 35);
            this.gadgetGrid.Name = "gadgetGrid";
            this.gadgetGrid.Size = new System.Drawing.Size(1086, 162);
            this.gadgetGrid.TabIndex = 0;
            // 
            // bAddGadget
            // 
            this.bAddGadget.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.bAddGadget.Location = new System.Drawing.Point(1016, 200);
            this.bAddGadget.Name = "bAddGadget";
            this.bAddGadget.Size = new System.Drawing.Size(75, 23);
            this.bAddGadget.TabIndex = 1;
            this.bAddGadget.Text = "Add gadget";
            this.bAddGadget.UseVisualStyleBackColor = true;
            this.bAddGadget.Click += new System.EventHandler(this.bAddGadget_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 16F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(12, 6);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(94, 26);
            this.label1.TabIndex = 2;
            this.label1.Text = "Gadgets";
            // 
            // bAddWidget
            // 
            this.bAddWidget.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.bAddWidget.Location = new System.Drawing.Point(1015, 572);
            this.bAddWidget.Name = "bAddWidget";
            this.bAddWidget.Size = new System.Drawing.Size(75, 23);
            this.bAddWidget.TabIndex = 4;
            this.bAddWidget.Text = "Add widget";
            this.bAddWidget.UseVisualStyleBackColor = true;
            this.bAddWidget.Click += new System.EventHandler(this.bAddWidget_Click);
            // 
            // widgetGrid
            // 
            this.widgetGrid.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.widgetGrid.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.widgetGrid.Location = new System.Drawing.Point(1, 229);
            this.widgetGrid.Name = "widgetGrid";
            this.widgetGrid.Size = new System.Drawing.Size(1090, 159);
            this.widgetGrid.TabIndex = 3;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Microsoft Sans Serif", 16F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label2.Location = new System.Drawing.Point(-4, 200);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(91, 26);
            this.label2.TabIndex = 5;
            this.label2.Text = "Widgets";
            // 
            // bInfo
            // 
            this.bInfo.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.bInfo.Location = new System.Drawing.Point(3, 572);
            this.bInfo.Name = "bInfo";
            this.bInfo.Size = new System.Drawing.Size(69, 23);
            this.bInfo.TabIndex = 6;
            this.bInfo.Text = "Info";
            this.bInfo.UseVisualStyleBackColor = true;
            this.bInfo.Click += new System.EventHandler(this.bMinorProblem_Click);
            // 
            // bHorrificException
            // 
            this.bHorrificException.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.bHorrificException.Location = new System.Drawing.Point(216, 572);
            this.bHorrificException.Name = "bHorrificException";
            this.bHorrificException.Size = new System.Drawing.Size(156, 23);
            this.bHorrificException.TabIndex = 7;
            this.bHorrificException.Text = "Generate Horrific Exception";
            this.bHorrificException.UseVisualStyleBackColor = true;
            this.bHorrificException.Click += new System.EventHandler(this.bHorrificException_Click);
            // 
            // bRemoveGadget
            // 
            this.bRemoveGadget.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.bRemoveGadget.Location = new System.Drawing.Point(912, 200);
            this.bRemoveGadget.Name = "bRemoveGadget";
            this.bRemoveGadget.Size = new System.Drawing.Size(98, 23);
            this.bRemoveGadget.TabIndex = 8;
            this.bRemoveGadget.Text = "Remove gadget";
            this.bRemoveGadget.UseVisualStyleBackColor = true;
            this.bRemoveGadget.Click += new System.EventHandler(this.bRemoveGadget_Click);
            // 
            // bRemoveWidget
            // 
            this.bRemoveWidget.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.bRemoveWidget.Location = new System.Drawing.Point(911, 572);
            this.bRemoveWidget.Name = "bRemoveWidget";
            this.bRemoveWidget.Size = new System.Drawing.Size(98, 23);
            this.bRemoveWidget.TabIndex = 9;
            this.bRemoveWidget.Text = "Remove widget";
            this.bRemoveWidget.UseVisualStyleBackColor = true;
            this.bRemoveWidget.Click += new System.EventHandler(this.bRemoveWidget_Click);
            // 
            // btnTraceScope
            // 
            this.btnTraceScope.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnTraceScope.Location = new System.Drawing.Point(378, 572);
            this.btnTraceScope.Name = "btnTraceScope";
            this.btnTraceScope.Size = new System.Drawing.Size(115, 23);
            this.btnTraceScope.TabIndex = 10;
            this.btnTraceScope.Text = "Test Trace Scope";
            this.btnTraceScope.UseVisualStyleBackColor = true;
            this.btnTraceScope.Click += new System.EventHandler(this.btnTraceScope_Click);
            // 
            // btn100
            // 
            this.btn100.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btn100.Location = new System.Drawing.Point(588, 572);
            this.btn100.Name = "btn100";
            this.btn100.Size = new System.Drawing.Size(83, 23);
            this.btn100.TabIndex = 11;
            this.btn100.Text = "100 Events";
            this.btn100.UseVisualStyleBackColor = true;
            this.btn100.Click += new System.EventHandler(this.btn100_Click);
            // 
            // btn1000
            // 
            this.btn1000.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btn1000.Location = new System.Drawing.Point(677, 572);
            this.btn1000.Name = "btn1000";
            this.btn1000.Size = new System.Drawing.Size(83, 23);
            this.btn1000.TabIndex = 12;
            this.btn1000.Text = "1000 Events";
            this.btn1000.UseVisualStyleBackColor = true;
            this.btn1000.Click += new System.EventHandler(this.btn1000_Click);
            // 
            // btn10
            // 
            this.btn10.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btn10.Location = new System.Drawing.Point(499, 572);
            this.btn10.Name = "btn10";
            this.btn10.Size = new System.Drawing.Size(83, 23);
            this.btn10.TabIndex = 13;
            this.btn10.Text = "10 Events";
            this.btn10.UseVisualStyleBackColor = true;
            this.btn10.Click += new System.EventHandler(this.btn10_Click);
            // 
            // chkSystem
            // 
            this.chkSystem.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.chkSystem.AutoSize = true;
            this.chkSystem.Location = new System.Drawing.Point(19, 601);
            this.chkSystem.Name = "chkSystem";
            this.chkSystem.Size = new System.Drawing.Size(60, 17);
            this.chkSystem.TabIndex = 14;
            this.chkSystem.Text = "System";
            this.chkSystem.UseVisualStyleBackColor = true;
            // 
            // chkWidgets
            // 
            this.chkWidgets.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.chkWidgets.AutoSize = true;
            this.chkWidgets.Location = new System.Drawing.Point(85, 601);
            this.chkWidgets.Name = "chkWidgets";
            this.chkWidgets.Size = new System.Drawing.Size(65, 17);
            this.chkWidgets.TabIndex = 15;
            this.chkWidgets.Text = "Widgets";
            this.chkWidgets.UseVisualStyleBackColor = true;
            // 
            // chkGadgets
            // 
            this.chkGadgets.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.chkGadgets.AutoSize = true;
            this.chkGadgets.Location = new System.Drawing.Point(150, 601);
            this.chkGadgets.Name = "chkGadgets";
            this.chkGadgets.Size = new System.Drawing.Size(66, 17);
            this.chkGadgets.TabIndex = 16;
            this.chkGadgets.Text = "Gadgets";
            this.chkGadgets.UseVisualStyleBackColor = true;
            // 
            // txtContent
            // 
            this.txtContent.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtContent.Location = new System.Drawing.Point(3, 394);
            this.txtContent.Multiline = true;
            this.txtContent.Name = "txtContent";
            this.txtContent.Size = new System.Drawing.Size(1088, 172);
            this.txtContent.TabIndex = 17;
            // 
            // btnStartHosting
            // 
            this.btnStartHosting.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnStartHosting.Location = new System.Drawing.Point(378, 597);
            this.btnStartHosting.Name = "btnStartHosting";
            this.btnStartHosting.Size = new System.Drawing.Size(115, 23);
            this.btnStartHosting.TabIndex = 18;
            this.btnStartHosting.Text = "Start Hosting Service";
            this.btnStartHosting.UseVisualStyleBackColor = true;
            this.btnStartHosting.Click += new System.EventHandler(this.btnStartHosting_Click);
            // 
            // btnStopHosting
            // 
            this.btnStopHosting.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnStopHosting.Location = new System.Drawing.Point(499, 597);
            this.btnStopHosting.Name = "btnStopHosting";
            this.btnStopHosting.Size = new System.Drawing.Size(115, 23);
            this.btnStopHosting.TabIndex = 19;
            this.btnStopHosting.Text = "Stop Hosting Service";
            this.btnStopHosting.UseVisualStyleBackColor = true;
            this.btnStopHosting.Click += new System.EventHandler(this.StopDiagnostics);
            // 
            // bNotice
            // 
            this.bNotice.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.bNotice.Location = new System.Drawing.Point(78, 572);
            this.bNotice.Name = "bNotice";
            this.bNotice.Size = new System.Drawing.Size(64, 23);
            this.bNotice.TabIndex = 20;
            this.bNotice.Text = "Notice";
            this.bNotice.UseVisualStyleBackColor = true;
            this.bNotice.Click += new System.EventHandler(this.bNotice_Click);
            // 
            // bWarn
            // 
            this.bWarn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.bWarn.Location = new System.Drawing.Point(148, 572);
            this.bWarn.Name = "bWarn";
            this.bWarn.Size = new System.Drawing.Size(64, 23);
            this.bWarn.TabIndex = 21;
            this.bWarn.Text = "Warn";
            this.bWarn.UseVisualStyleBackColor = true;
            this.bWarn.Click += new System.EventHandler(this.bWarn_Click);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1094, 623);
            this.Controls.Add(this.bWarn);
            this.Controls.Add(this.bNotice);
            this.Controls.Add(this.btnStopHosting);
            this.Controls.Add(this.btnStartHosting);
            this.Controls.Add(this.txtContent);
            this.Controls.Add(this.chkGadgets);
            this.Controls.Add(this.chkWidgets);
            this.Controls.Add(this.chkSystem);
            this.Controls.Add(this.btn10);
            this.Controls.Add(this.btn1000);
            this.Controls.Add(this.btn100);
            this.Controls.Add(this.btnTraceScope);
            this.Controls.Add(this.bRemoveWidget);
            this.Controls.Add(this.bRemoveGadget);
            this.Controls.Add(this.bHorrificException);
            this.Controls.Add(this.bInfo);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.bAddWidget);
            this.Controls.Add(this.widgetGrid);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.bAddGadget);
            this.Controls.Add(this.gadgetGrid);
            this.Name = "Form1";
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.Text = "WidgetSample";
            ((System.ComponentModel.ISupportInitialize)(this.gadgetGrid)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.widgetGrid)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.DataGridView gadgetGrid;
		private System.Windows.Forms.Button bAddGadget;
		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.Button bAddWidget;
		private System.Windows.Forms.DataGridView widgetGrid;
		private System.Windows.Forms.Label label2;
		private System.Windows.Forms.Button bInfo;
		private System.Windows.Forms.Button bHorrificException;
		private System.Windows.Forms.Button bRemoveGadget;
		private System.Windows.Forms.Button bRemoveWidget;
		private System.Windows.Forms.Button btnTraceScope;
		private System.Windows.Forms.Button btn100;
		private System.Windows.Forms.Button btn1000;
		private System.Windows.Forms.Button btn10;
        private System.Windows.Forms.CheckBox chkSystem;
        private System.Windows.Forms.CheckBox chkWidgets;
        private System.Windows.Forms.CheckBox chkGadgets;
				private System.Windows.Forms.TextBox txtContent;
		private System.Windows.Forms.Button btnStartHosting;
		private System.Windows.Forms.Button btnStopHosting;
        private System.Windows.Forms.Button bNotice;
        private System.Windows.Forms.Button bWarn;
    }
}

