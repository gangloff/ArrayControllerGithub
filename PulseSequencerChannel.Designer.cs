using System;
namespace ArrayDACControl
{
    partial class PulseSequencerChannel
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.panel1 = new System.Windows.Forms.Panel();
            this.opt3 = new System.Windows.Forms.RadioButton();
            this.opt2 = new System.Windows.Forms.RadioButton();
            this.opt1 = new System.Windows.Forms.RadioButton();
            this.inplabel3 = new System.Windows.Forms.Label();
            this.inplabel2 = new System.Windows.Forms.Label();
            this.inplabel1 = new System.Windows.Forms.Label();
            this.inplabel0 = new System.Windows.Forms.Label();
            this.SigParam1 = new System.Windows.Forms.TextBox();
            this.SigParam3 = new System.Windows.Forms.TextBox();
            this.SigParam2 = new System.Windows.Forms.TextBox();
            this.SigName = new System.Windows.Forms.TextBox();
            this.chlabel = new System.Windows.Forms.Label();
            this.waveformGraph1 = new NationalInstruments.UI.WindowsForms.WaveformGraph();
            this.waveformPlot1 = new NationalInstruments.UI.WaveformPlot();
            this.xAxis1 = new NationalInstruments.UI.XAxis();
            this.yAxis1 = new NationalInstruments.UI.YAxis();
            this.panel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.waveformGraph1)).BeginInit();
            this.SuspendLayout();
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.opt3);
            this.panel1.Controls.Add(this.opt2);
            this.panel1.Controls.Add(this.opt1);
            this.panel1.Location = new System.Drawing.Point(382, 5);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(147, 21);
            this.panel1.TabIndex = 189;
            // 
            // opt3
            // 
            this.opt3.AutoSize = true;
            this.opt3.Location = new System.Drawing.Point(93, 3);
            this.opt3.Name = "opt3";
            this.opt3.Size = new System.Drawing.Size(46, 17);
            this.opt3.TabIndex = 180;
            this.opt3.Text = "opt3";
            this.opt3.UseVisualStyleBackColor = true;
            this.opt3.CheckedChanged += new System.EventHandler(this.opt3_CheckedChanged);
            // 
            // opt2
            // 
            this.opt2.AutoSize = true;
            this.opt2.Location = new System.Drawing.Point(49, 3);
            this.opt2.Name = "opt2";
            this.opt2.Size = new System.Drawing.Size(46, 17);
            this.opt2.TabIndex = 179;
            this.opt2.Text = "opt2";
            this.opt2.UseVisualStyleBackColor = true;
            this.opt2.CheckedChanged += new System.EventHandler(this.opt2_CheckedChanged);
            // 
            // opt1
            // 
            this.opt1.AutoSize = true;
            this.opt1.Checked = true;
            this.opt1.Location = new System.Drawing.Point(3, 3);
            this.opt1.Name = "opt1";
            this.opt1.Size = new System.Drawing.Size(40, 17);
            this.opt1.TabIndex = 10;
            this.opt1.TabStop = true;
            this.opt1.Text = "Ind";
            this.opt1.UseVisualStyleBackColor = true;
            this.opt1.CheckedChanged += new System.EventHandler(this.opt1_CheckedChanged);
            // 
            // inplabel3
            // 
            this.inplabel3.AutoSize = true;
            this.inplabel3.Font = new System.Drawing.Font("Microsoft Sans Serif", 6.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.inplabel3.ForeColor = System.Drawing.Color.Firebrick;
            this.inplabel3.Location = new System.Drawing.Point(295, 29);
            this.inplabel3.Name = "inplabel3";
            this.inplabel3.Size = new System.Drawing.Size(36, 12);
            this.inplabel3.TabIndex = 188;
            this.inplabel3.Text = "param3";
            // 
            // inplabel2
            // 
            this.inplabel2.AutoSize = true;
            this.inplabel2.Font = new System.Drawing.Font("Microsoft Sans Serif", 6.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.inplabel2.ForeColor = System.Drawing.Color.Firebrick;
            this.inplabel2.Location = new System.Drawing.Point(210, 29);
            this.inplabel2.Name = "inplabel2";
            this.inplabel2.Size = new System.Drawing.Size(36, 12);
            this.inplabel2.TabIndex = 187;
            this.inplabel2.Text = "param2";
            // 
            // inplabel1
            // 
            this.inplabel1.AutoSize = true;
            this.inplabel1.Font = new System.Drawing.Font("Microsoft Sans Serif", 6.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.inplabel1.ForeColor = System.Drawing.Color.Firebrick;
            this.inplabel1.Location = new System.Drawing.Point(125, 29);
            this.inplabel1.Name = "inplabel1";
            this.inplabel1.Size = new System.Drawing.Size(36, 12);
            this.inplabel1.TabIndex = 186;
            this.inplabel1.Text = "param1";
            // 
            // inplabel0
            // 
            this.inplabel0.AutoSize = true;
            this.inplabel0.Font = new System.Drawing.Font("Microsoft Sans Serif", 6.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.inplabel0.ForeColor = System.Drawing.Color.Firebrick;
            this.inplabel0.Location = new System.Drawing.Point(57, 29);
            this.inplabel0.Name = "inplabel0";
            this.inplabel0.Size = new System.Drawing.Size(28, 12);
            this.inplabel0.TabIndex = 185;
            this.inplabel0.Text = "name";
            // 
            // SigParam1
            // 
            this.SigParam1.Location = new System.Drawing.Point(127, 6);
            this.SigParam1.Name = "SigParam1";
            this.SigParam1.Size = new System.Drawing.Size(79, 20);
            this.SigParam1.TabIndex = 184;
            this.SigParam1.Text = "200";
            this.SigParam1.LostFocus += new System.EventHandler(this.SigParam1_Enter);
            // 
            // SigParam3
            // 
            this.SigParam3.Location = new System.Drawing.Point(297, 6);
            this.SigParam3.Name = "SigParam3";
            this.SigParam3.Size = new System.Drawing.Size(79, 20);
            this.SigParam3.TabIndex = 183;
            this.SigParam3.Text = "0";
            this.SigParam3.LostFocus += new System.EventHandler(this.SigParam3_Enter);
            // 
            // SigParam2
            // 
            this.SigParam2.Location = new System.Drawing.Point(212, 6);
            this.SigParam2.Name = "SigParam2";
            this.SigParam2.Size = new System.Drawing.Size(79, 20);
            this.SigParam2.TabIndex = 182;
            this.SigParam2.Text = "200";
            this.SigParam2.LostFocus += new System.EventHandler(this.SigParam2_Enter);
            // 
            // SigName
            // 
            this.SigName.Location = new System.Drawing.Point(57, 6);
            this.SigName.Name = "SigName";
            this.SigName.Size = new System.Drawing.Size(64, 20);
            this.SigName.TabIndex = 181;
            this.SigName.Text = "name";
            // 
            // chlabel
            // 
            this.chlabel.AutoSize = true;
            this.chlabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.chlabel.ForeColor = System.Drawing.Color.Red;
            this.chlabel.Location = new System.Drawing.Point(8, 8);
            this.chlabel.Name = "chlabel";
            this.chlabel.Size = new System.Drawing.Size(43, 20);
            this.chlabel.TabIndex = 191;
            this.chlabel.Text = "ch #";
            // 
            // waveformGraph1
            // 
            this.waveformGraph1.BackgroundImageAlignment = NationalInstruments.UI.ImageAlignment.Center;
            this.waveformGraph1.Border = NationalInstruments.UI.Border.None;
            this.waveformGraph1.CaptionVisible = false;
            this.waveformGraph1.Location = new System.Drawing.Point(535, 0);
            this.waveformGraph1.Name = "waveformGraph1";
            this.waveformGraph1.PlotAreaBorder = NationalInstruments.UI.Border.None;
            this.waveformGraph1.PlotAreaColor = System.Drawing.Color.WhiteSmoke;
            this.waveformGraph1.Plots.AddRange(new NationalInstruments.UI.WaveformPlot[] {
            this.waveformPlot1});
            this.waveformGraph1.Size = new System.Drawing.Size(265, 47);
            this.waveformGraph1.TabIndex = 192;
            this.waveformGraph1.UseColorGenerator = true;
            this.waveformGraph1.XAxes.AddRange(new NationalInstruments.UI.XAxis[] {
            this.xAxis1});
            this.waveformGraph1.YAxes.AddRange(new NationalInstruments.UI.YAxis[] {
            this.yAxis1});
            // 
            // waveformPlot1
            // 
            this.waveformPlot1.LineColor = System.Drawing.Color.Red;
            this.waveformPlot1.LineColorPrecedence = NationalInstruments.UI.ColorPrecedence.UserDefinedColor;
            this.waveformPlot1.LineWidth = 2F;
            this.waveformPlot1.XAxis = this.xAxis1;
            this.waveformPlot1.YAxis = this.yAxis1;
            // 
            // xAxis1
            // 
            this.xAxis1.MinorDivisions.GridLineStyle = NationalInstruments.UI.LineStyle.Dot;
            this.xAxis1.MinorDivisions.GridVisible = true;
            this.xAxis1.MinorDivisions.Interval = 100;
            this.xAxis1.Visible = false;
            // 
            // yAxis1
            // 
            this.yAxis1.Mode = NationalInstruments.UI.AxisMode.Fixed;
            this.yAxis1.Range = new NationalInstruments.UI.Range(-0.1, 1.1);
            this.yAxis1.Visible = false;
            // 
            // PulseSequencerChannel
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.waveformGraph1);
            this.Controls.Add(this.chlabel);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.inplabel3);
            this.Controls.Add(this.inplabel2);
            this.Controls.Add(this.inplabel1);
            this.Controls.Add(this.inplabel0);
            this.Controls.Add(this.SigParam1);
            this.Controls.Add(this.SigParam3);
            this.Controls.Add(this.SigParam2);
            this.Controls.Add(this.SigName);
            this.Name = "PulseSequencerChannel";
            this.Size = new System.Drawing.Size(800, 47);
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.waveformGraph1)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.RadioButton opt3;
        private System.Windows.Forms.RadioButton opt2;
        private System.Windows.Forms.RadioButton opt1;
        private System.Windows.Forms.Label inplabel3;
        private System.Windows.Forms.Label inplabel2;
        private System.Windows.Forms.Label inplabel1;
        private System.Windows.Forms.Label inplabel0;
        private System.Windows.Forms.TextBox SigParam1;
        private System.Windows.Forms.TextBox SigParam3;
        private System.Windows.Forms.TextBox SigParam2;
        private System.Windows.Forms.TextBox SigName;
        private System.Windows.Forms.Label chlabel;
        private NationalInstruments.UI.WindowsForms.WaveformGraph waveformGraph1;
        private NationalInstruments.UI.WaveformPlot waveformPlot1;
        private NationalInstruments.UI.XAxis xAxis1;
        private NationalInstruments.UI.YAxis yAxis1;

    }
}
