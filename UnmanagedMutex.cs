using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace ArrayDACControl
{
    class UnmanagedMutex
    {
        // Use interop to call the CreateMutex function.
        // For more information about CreateMutex,
        // see the unmanaged MSDN reference library.
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        static extern SafeWaitHandle CreateMutex(IntPtr lpMutexAttributes, bool bInitialOwner,
        string lpName);


        // Use interop to call the ReleaseMutex function.
        // For more information about ReleaseMutex,
        // see the unmanaged MSDN reference library.
        [DllImport("kernel32.dll")]
        public static extern bool ReleaseMutex(SafeWaitHandle hMutex);



        private SafeWaitHandle handleValue = null;
        private IntPtr mutexAttrValue = IntPtr.Zero;
        private string nameValue = null;

        public UnmanagedMutex(string Name)
        {
            nameValue = Name;
        }


        public void Create()
        {
            if (nameValue == null && nameValue.Length == 0)
            {
                throw new ArgumentNullException("nameValue");
            }

            handleValue = CreateMutex(mutexAttrValue,
                                            true, nameValue);

            // If the handle is invalid,
            // get the last Win32 error 
            // and throw a Win32Exception.
            if (handleValue.IsInvalid)
            {
                Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
            }
        }

        public SafeWaitHandle Handle
        {
            get
            {
                // If the handle is valid,
                // return it.
                if (!handleValue.IsInvalid)
                {
                    return handleValue;
                }
                else
                {
                    return null;
                }
            }

        }

        public string Name
        {
            get
            {
                return nameValue;
            }

        }


        public void Release()
        {
            ReleaseMutex(handleValue);
        }
    }
}
