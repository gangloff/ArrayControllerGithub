using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Text;
using System.Windows.Forms;

namespace ArrayDACControl
{
    
    public partial class AdjustableSlider : UserControl
    {
        double val;

        //Virtual slider minima and maxima
        int trackmin, trackmax;

        //Current Maxima and Minima
        private double min, max;

        //Absolute Minima and Maxima
        private double absmin, absmax;

        //Constructor
        public AdjustableSlider()
        {
            InitializeComponent();
            this.trackBar1.Minimum = -32000;
            this.trackBar1.Maximum = +32000;
            trackmin = -32000;
            trackmax = 32000;
        }

        //Slider Label
        public String SliderLabel
        {
            get {
                return groupBox1.Text;
            }
            set { 
                this.groupBox1.Text = value;                 
            }
        }
            // Declare the event, which is associated with our
            // delegate SubmitClickedHandler(). Add some attributes
            // for the Visual C# control property.
         public delegate void SliderAdjustHandler(object sender, EventArgs e);

         [Category("Action")]
         [Description("Fires when the slider is adjusted.")]
         public event SliderAdjustHandler SliderAdjusted;


         public double AbsMin
         {
             get { return absmin; }
             set
             {
                 absmin = value;
                 label1.Text = "Min " + absmin.ToString("F0");
             }
         }
         public double AbsMax
         {
             get { return absmax; }
             set
             {
                 absmax = value;
                 label2.Text = "Max " + absmax.ToString("F0");
             }
         }

         public double Min
         {
             get
             {
                 try
                 {
                     min = System.Convert.ToDouble(textBox1.Text);
                 }
                 catch (Exception ex) {MessageBox.Show(ex.Message); }
                 return min;
             }
             set
             {
                 min = value;
                 if (min < absmin) { min = absmin; }
                 textBox1.Text = min.ToString("F3");
             }
         }
         public double Max
         {
             get
             {
                 try
                 {
                     max = System.Convert.ToDouble(textBox2.Text);
                 }
                 catch (Exception ex) { MessageBox.Show(ex.Message); }
                 return max;
             }
             set
             {
                 max = value;
                 if (max > absmax) { max = absmax; }
                 textBox2.Text = max.ToString("F3");
             }
         }


        // Read / Write Property for the User Name. This Property
        // will be visible in the containing application.
        [Category("Appearance")]
        [Description("Gets or sets the slider value")]
        public double Value
        {
            get { return val; }
            set { 
                val = value; 

                if (val > max)
                {
                    Max = absmax;
                }
                if (val < min)
                {
                    Min = absmin;
                }
                if (val > absmax)
                {
                    val = absmax;
                }
                if (val < absmin)
                {
                    val = absmin;
                }

                int off;
                if (max != min)
                {
                    off = (int)(((val - min) / (max - min)) * (trackmax - trackmin));
                }
                else
                {
                    off = 0;
                }
                trackBar1.Value = trackmin+off;
                textBox3.Text = val.ToString("F3");
            }
        }



        
        private void trackBar1_Scroll(object sender, EventArgs e)
        {
          
            int track = trackBar1.Value;
            
            val = ((double)(track-trackmin))/(trackmax - trackmin);            
            val = Min + (Max - Min) * val;

            Value = val;           
            
            if(SliderAdjusted!=null)
               SliderAdjusted(sender,e);
        }

        //Event handler for changing value of slider limits
        private void textBox_Enter(object sender, EventArgs e)
        {
            
            //get values from textboxes
            try
            {
                min = System.Convert.ToDouble(textBox1.Text);
                max = System.Convert.ToDouble(textBox2.Text);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }

            //error handling for improper user-entered limits on slider
            if (min > max)
            {
                double temp = max;
                max = min;
                min = temp;
            }

            if ((min + 0.000001) > max)
            {
                max = min + 0.01;
            }

            if (min > val)
            {
                min = val;
            }

            if (max < val)
            {
                max = val;
            }

            //update
            Max = max;
            Min = min;
            
            //compute new value of slide position, and update position
            int off = (int)((val - min) / (max - min) * (trackmax - trackmin));
            trackBar1.Value = off+trackmin;
        }

        private void textBox3_TextChanged(object sender, EventArgs e)
        {
            try
            {
                Value = double.Parse(textBox3.Text);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }

            if (SliderAdjusted != null)
                SliderAdjusted(sender, e);
        }

    }
}
