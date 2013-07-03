using System;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;
using System.Data;
using NationalInstruments.NI4882;


namespace ArrayDACControl
{
    public class GPIB
    {
        private Device device = null;
        public String stringRead;

        public GPIB(int boardNumber, byte primaryAddress)
        {
            try
            {
                device = new Device(boardNumber, primaryAddress);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            device.SynchronizeCallbacks = true;
        }

        public void EndDevice()
        {
            device.Dispose();
        }

        public void Abort()
        {
            device.AbortAsynchronousIO();
        }

        public void simpleRead(int bytes)
        {
            try
            {
                stringRead = device.ReadString(bytes);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        public void Read()
        {
            try
            {
                device.BeginRead(
                    new AsyncCallback(OnReadComplete),
                    null);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        public void Clear()
        {
            try
            {
                device.Clear();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        public void Reset()
        {
            try
            {
                device.Reset();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void OnReadComplete(IAsyncResult result)
        {
            try
            {
                stringRead = device.EndReadString(result);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }


    };
}
