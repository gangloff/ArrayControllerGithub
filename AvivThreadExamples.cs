using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace ArrayDACControl
{
    class AvivThreadExamples
    {
        private object lockObject = new object();

        private void thread1()
        {
            while (true)
            {
                // do stuff -- time consuming operation



                lock (lockObject)
                {
                    // acquire image
                    // super slow


                    // tell thread 2 to do stuff
                    Monitor.PulseAll(lockObject);


                }
                    
            }
        }

        public void thread2()
        {
            while (true)
            {
                // other slow thing

                lock (lockObject)
                {
                    Monitor.Wait(lockObject);
                    
                    // normalize image
                    // use data
                    // semi-slow
                }
                // wait to hear from thread 1
            }
        }

        private void threadExample()
        {
            while (true)
            {
                // wait for thread 1 to tell me to do stuff

                // wait forever
                lock (lockObject)
                {
                    // do stuff that requires lock on lockObject
                }


                // wait 500ms
                bool entered = Monitor.TryEnter(lockObject, 500);
                try
                {
                    
                    if (entered)
                    {
                        // do stuff
                    }
                }
                finally
                {
                    if (entered) Monitor.Exit(lockObject);
                }
                // do stuff -- less time consuming operation
            }
        }

        // running on a random thread
        private void anonymousFunctionExample(Form someForm)
        {

            string a = "sometext";
            //someForm.BeginInvoke(delegate() { someForm.Text = a; });



        }
    }
}
