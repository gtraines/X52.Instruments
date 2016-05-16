﻿using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Microsoft.Win32;

using HResult = System.Int32;

namespace X52.Adapter
{

    public struct SRequestStatus
    {
        public Int32 headerError;
        public Int32 headerInfo;
        public Int32 requestError;
        public Int32 requestInfo;
    };

    public class RegistryKeyNotFound : Exception
    {
        public RegistryKeyNotFound() : base(@"HKEY_LOCAL_MACHINE\SOFTWARE\Saitek\DirectOutput key not found.") { }
    }

    public class RegistryValueNotFound : Exception
    {
        public RegistryValueNotFound() : base(@"DirectOutput value in key HKEY_LOCAL_MACHINE\SOFTWARE\Saitek\DirectOutput not found.") { }
    }

    public class SRequestStatusException : Exception
    {

        public SRequestStatus requestStatus;

        public SRequestStatusException(SRequestStatus requestStatus)
        {
            this.requestStatus = requestStatus;
        }

    }

    public class HResultException : Exception
    {

        public const HResult S_OK = 0x00000000;
        public const HResult E_OUTOFMEMORY = unchecked((HResult)0x8007000E);
        public const HResult E_NOTIMPL = unchecked((HResult)0x80004001);
        public const HResult E_INVALIDARG = unchecked((HResult)0x80070057);
        public const HResult E_PAGENOTACTIVE = unchecked((HResult)0xFF040001);
        public const HResult E_HANDLE = unchecked((HResult)0x80070006);
        public const HResult E_UNKNOWN_1 = unchecked((HResult)0x51B87CE3);

        public HResultException(HResult result, Dictionary<HResult, String> errorsMap)
            : base(errorsMap[result])
        {
            HResult = result;
        }
    }

    public class DirectOutput
    {

        public const Int32 IsActive = 0x00000001;

        // Callbacks
        public delegate void EnumerateCallback(IntPtr device, IntPtr target);
        public delegate void DeviceCallback(IntPtr device, bool added, IntPtr target);
        public delegate void SoftButtonCallback(IntPtr device, UInt32 buttons, IntPtr target);
        public delegate void PageCallback(IntPtr device, Int32 page, bool activated, IntPtr target);

        // Library functions
        private delegate HResult DirectOutput_Initialize([MarshalAsAttribute(UnmanagedType.LPWStr)] String appName);
        private delegate HResult DirectOutput_Deinitialize();
        private delegate HResult DirectOutput_RegisterDeviceChangeCallback([MarshalAs(UnmanagedType.FunctionPtr)]DeviceCallback callback, IntPtr target);
        private delegate HResult DirectOutput_Enumerate([MarshalAs(UnmanagedType.FunctionPtr)]EnumerateCallback callback, IntPtr target);
        private delegate HResult DirectOutput_GetDeviceType(IntPtr device, out Guid guidType);
        private delegate HResult DirectOutput_GetDeviceInstance(IntPtr device, out Guid guidInstance);
        private delegate HResult DirectOutput_SetProfile(IntPtr device, Int32 fileNameLength, [MarshalAsAttribute(UnmanagedType.LPWStr)] String filename);
        private delegate HResult DirectOutput_RegisterSoftButtonChangeCallback(IntPtr device, [MarshalAs(UnmanagedType.FunctionPtr)]SoftButtonCallback callback, IntPtr target);
        private delegate HResult DirectOutput_RegisterPageChangeCallback(IntPtr device, [MarshalAs(UnmanagedType.FunctionPtr)]PageCallback callback, IntPtr target);
        private delegate HResult DirectOutput_AddPage(IntPtr device, Int32 page, Int32 flags);
        private delegate HResult DirectOutput_RemovePage(IntPtr device, Int32 page);
        private delegate HResult DirectOutput_SetLed(IntPtr device, Int32 page, Int32 index, Int32 value);
        private delegate HResult DirectOutput_SetString(IntPtr device, Int32 page, Int32 index, Int32 valueLength, [MarshalAsAttribute(UnmanagedType.LPWStr)] String value);
        private delegate HResult DirectOutput_SetImage(IntPtr device, Int32 page, Int32 index, Int32 bufferlength, byte[] buffer);

        // Functions placeholders
        private DirectOutput_AddPage addPage;
        private DirectOutput_Initialize initialize;
        private DirectOutput_Deinitialize deinitialize;
        private DirectOutput_Enumerate enumerate;
        private DirectOutput_GetDeviceType getDeviceType;
        private DirectOutput_GetDeviceInstance getDeviceInstance;
        private DirectOutput_RegisterDeviceChangeCallback registerDeviceChangeCallback;
        private DirectOutput_RegisterSoftButtonChangeCallback registerSoftButtonChangeCallback;
        private DirectOutput_RegisterPageChangeCallback registerPageChangeCallback;
        private DirectOutput_RemovePage removePage;
        private DirectOutput_SetImage setImage;
        private DirectOutput_SetLed setLed;
        private DirectOutput_SetProfile setProfile;
        private DirectOutput_SetString setString;
        

        private const String directOutputKey = "SOFTWARE\\Saitek\\DirectOutput";

        private IntPtr hModule;

        /// <summary>
        /// Creates DirectOutput wrapper
        /// </summary>
        /// <param name="libPath">Path to DirectOutput.dll</param>
        /// <exception cref="RegistryKeyNotFound">HKEY_LOCAL_MACHINE\SOFTWARE\Saitek\DirectOutput key not found. Usualy, this mean what Saitek Drivers not installed properly.</exception>
        /// <exception cref="RegistryValueNotFound">DirectOutput value in key HKEY_LOCAL_MACHINE\SOFTWARE\Saitek\DirectOutput key not found. Usualy, this mean what Saitek Drivers not installed properly.</exception>
        /// <exception cref="OutOfMemoryException">The system is out of memory or resources.</exception>
        /// <exception cref="BadFormatException">The target file is invalid.</exception>
        /// <exception cref="FileNotFountException">The specified file was not found.</exception>
        /// <exception cref="PathNotFountException">The specified path was not found.</exception>
        /// <exception cref="UnknownException">Unknown exception during library loading.</exception>
        public DirectOutput(String libPath = null)
        {
            if (libPath == null)
            {
                RegistryKey key = Registry.LocalMachine.OpenSubKey(directOutputKey);
                if (key == null)
                {
                    throw new RegistryKeyNotFound();
                }

                object value = key.GetValue("DirectOutput");
                if ((value == null) || !(value is String))
                {
                    throw new RegistryValueNotFound();
                }

                libPath = (String)value;
            }
            hModule = DllHelper.LoadLibrary(libPath);

            InitializeLibraryFunctions();
        }

        ~DirectOutput()
        {
            DllHelper.FreeLibrary(hModule);
        }

        private void InitializeLibraryFunctions()
        {
            initialize = DllHelper.GetFunction<DirectOutput_Initialize>(hModule, "DirectOutput_Initialize");
            deinitialize = DllHelper.GetFunction<DirectOutput_Deinitialize>(hModule, "DirectOutput_Deinitialize");

            enumerate = DllHelper.GetFunction<DirectOutput_Enumerate>(hModule, "DirectOutput_Enumerate");
            getDeviceType = DllHelper.GetFunction<DirectOutput_GetDeviceType>(hModule, "DirectOutput_GetDeviceType");
            getDeviceInstance = DllHelper.GetFunction<DirectOutput_GetDeviceInstance>(hModule, "DirectOutput_GetDeviceInstance");
            setProfile = DllHelper.GetFunction<DirectOutput_SetProfile>(hModule, "DirectOutput_SetProfile");
            registerDeviceChangeCallback =
                DllHelper.GetFunction<DirectOutput_RegisterDeviceChangeCallback>(
                    hModule,
                    "DirectOutput_RegisterDeviceChangeCallback");
            registerSoftButtonChangeCallback = 
                DllHelper.GetFunction<DirectOutput_RegisterSoftButtonChangeCallback>(
                    hModule, 
                    "DirectOutput_RegisterSoftButtonChangeCallback");
            registerPageChangeCallback = 
                DllHelper.GetFunction<DirectOutput_RegisterPageChangeCallback>(
                    hModule, 
                    "DirectOutput_RegisterPageChangeCallback");
            addPage = DllHelper.GetFunction<DirectOutput_AddPage>(hModule, "DirectOutput_AddPage");
            removePage = DllHelper.GetFunction<DirectOutput_RemovePage>(hModule, "DirectOutput_RemovePage");
            setLed = DllHelper.GetFunction<DirectOutput_SetLed>(hModule, "DirectOutput_SetLed");
            setString = DllHelper.GetFunction<DirectOutput_SetString>(hModule, "DirectOutput_SetString");
            setImage = DllHelper.GetFunction<DirectOutput_SetImage>(hModule, "DirectOutput_SetImage");
            
        }

        /// <summary>
        /// Initialize the DirectOutput library.
        /// </summary>
        /// <param name="appName">String that specifies the name of the application. Optional</param>
        /// <remarks>
        /// This function must be called before calling any others. Call this function when you want to initialize the DirectOutput library.
        /// </remarks>
        /// <exception cref="HResultException"></exception>
        public void Initialize(String appName = "DirectOutputCSharpWrapper")
        {
            HResult retVal = initialize(appName);
            if (retVal != HResultException.S_OK)
            {
                Dictionary<HResult, String> errorsMap = new Dictionary<HResult, String>() {
                    {HResultException.E_OUTOFMEMORY, "There was insufficient memory to complete this call."},
                    {HResultException.E_INVALIDARG, "The argument is invalid."},
                    {HResultException.E_HANDLE, "The DirectOutputManager prcess could not be found."}
                };
                throw new HResultException(retVal, errorsMap);
            }
        }

        /// <summary>
        /// Clean up the DirectOutput library.
        /// </summary>
        /// <remarks>
        /// This function must be called before termination. Call this function to clean up any resources allocated by <see cref="Initialize"/> .
        /// </remarks>
        /// <exception cref="HResultException"></exception>
        public void Deinitialize()
        {
            HResult retVal = deinitialize();
            if (retVal != HResultException.S_OK)
            {
                Dictionary<HResult, String> errorsMap = new Dictionary<HResult, String>() {
                    {HResultException.E_HANDLE, "DirectOutput was not initialized or was already deinitialized."}
                };
                throw new HResultException(retVal, errorsMap);
            }
        }

        /// <summary>
        /// Register a callback function to be called when a device is added or removed.
        /// </summary>
        /// <param name="callback">Callback delegate to be called whenever a device is added or removed</param>
        /// <remarks>
        /// Passing a NULL function pointer will disable the callback.
        /// </remarks>
        /// <exception cref="HResultException"></exception>
        public void RegisterDeviceChangeCallback(DeviceCallback callback)
        {
            HResult retVal = registerDeviceChangeCallback(callback, new IntPtr());
            if (retVal != HResultException.S_OK)
            {
                Dictionary<HResult, String> errorsMap = new Dictionary<HResult, String>() {
                    {HResultException.E_HANDLE, "DirectOutput was not initialized."}
                };
                throw new HResultException(retVal, errorsMap);
            }
        }

        /// <summary>
        /// Enumerate all currently attached DirectOutput devices.
        /// </summary>
        /// <param name="callback">Callback delegate to be called for each detected device.</param>
        /// <remarks>
        /// This function has changed from previous releases. 
        /// </remarks>
        /// <exception cref="HResultException"></exception>
        public void Enumerate(EnumerateCallback callback)
        {
            HResult retVal = enumerate(callback, new IntPtr());
            if (retVal != HResultException.S_OK)
            {
                Dictionary<HResult, String> errorsMap = new Dictionary<HResult, String>() {
                    {HResultException.E_HANDLE, "DirectOutput was not initialized."}
                };
                throw new HResultException(retVal, errorsMap);
            }
        }

        /// <summary>
        /// Gets an identifier that identifies the device.
        /// </summary>
        /// <param name="device">Handle that was supplied in the device change callback.</param>
        /// <returns>Guid value that will recieve the type identifier of this device.</returns>
        /// <remarks>
        /// Refer to the list of type GUIDs to find out about what features are available on each device.
        /// </remarks>
        /// <exception cref="HResultException"></exception>
        public Guid GetDeviceType(IntPtr device)
        {
            Guid guidType;
            HResult retVal = getDeviceType(device, out guidType);
            if (retVal != HResultException.S_OK)
            {
                Dictionary<HResult, String> errorsMap = new Dictionary<HResult, String>() {
                    {HResultException.E_INVALIDARG, "An argument is invalid."},
                    {HResultException.E_HANDLE, "The device handle specified is invalid."}
                };
                throw new HResultException(retVal, errorsMap);
            }
            return guidType;
        }

        /// <summary>
        /// Gets an instance identifier to used with Microsoft DirectInput.
        /// </summary>
        /// <param name="device">Handle that was supplied in the device change callback.</param>
        /// <returns>Guid value that will recieve the instance identifier of this device.</returns>
        /// <remarks>
        /// Use guid in IDirectInput::CreateDevice to create the IDirectInputDevice that corrresponds to this DirectOutput device.
        /// </remarks>
        /// <exception cref="HResultException"></exception>
        public Guid GetDeviceInstance(IntPtr device)
        {
            Guid guidType;
            HResult retVal = getDeviceInstance(device, out guidType);
            if (retVal != HResultException.S_OK)
            {
                Dictionary<HResult, String> errorsMap = new Dictionary<HResult, String>() {
                    {HResultException.E_NOTIMPL, "This device does not support DirectInput."},
                    {HResultException.E_INVALIDARG, "An argument is invalid."},
                    {HResultException.E_HANDLE, "The device handle specified is invalid."}
                };
                throw new HResultException(retVal, errorsMap);
            }
            return guidType;
        }

        /// <summary>
        /// Sets the profile on this device.
        /// </summary>
        /// <param name="device">A handle to a device.</param>
        /// <param name="fileName">Full path and filename of the profile to activate.</param>
        /// <remarks>
        /// Passing in a null to fileName will clear the current profile. 
        /// </remarks>
        /// <exception cref="HResultException"></exception>
        public void SetProfile(IntPtr device, String fileName)
        {
            Int32 fileNameLength = fileName != null ? fileName.Length : 0;
            HResult retVal = setProfile(device, fileNameLength, fileName);
            if (retVal != HResultException.S_OK)
            {
                Dictionary<HResult, String> errorsMap = new Dictionary<HResult, String>() {
                    {HResultException.E_NOTIMPL, "The device does not support profiling."},
                    {HResultException.E_INVALIDARG, "An argument is invalid."},
                    {HResultException.E_OUTOFMEMORY, "Insufficient memory to complete the request."},
                    {HResultException.E_HANDLE, "The device handle specified is invalid."}
                };
                throw new HResultException(retVal, errorsMap);
            }
        }

        /// <summary>
        /// Registers a callback with a device, that gets called whenever a "Soft Button" is pressed or released.
        /// </summary>
        /// <param name="device">A handle to a device.</param>
        /// <param name="callback">Callback delegate to be called whenever a "Soft Button" is pressed or released.</param>
        /// <remarks>
        /// Passing in a null to callback will disable the callback. 
        /// </remarks>
        /// <exception cref="HResultException"></exception>
        public void RegisterSoftButtonChangeCallback(IntPtr device, SoftButtonCallback callback)
        {
            HResult retVal = registerSoftButtonChangeCallback(device, callback, new IntPtr());
            if (retVal != HResultException.S_OK)
            {
                Dictionary<HResult, String> errorsMap = new Dictionary<HResult, String>() {
                    {HResultException.E_HANDLE, "The device handle specified is invalid."}
                };
                throw new HResultException(retVal, errorsMap);
            }
        }

        /// <summary>
        /// Registers a callback with a device, that gets called whenever the active page is changed.
        /// </summary>
        /// <param name="device">A handle to a device.</param>
        /// <param name="callback">Callback delegate to be called whenever the active page is changed.</param>
        /// <remarks>
        /// Adding a page with an existing page id is not allowed. The page id only has to be unique on a per application basis. The callback
        /// will not be called when a page is added as the active page with a call to AddPage(device, page, name, DirectOutput.IsActive);
        /// Passing a null to callback will disable the callback.
        /// </remarks>
        /// <exception cref="HResultException"></exception>
        public void RegisterPageCallback(IntPtr device, PageCallback callback)
        {
            HResult retVal = registerPageChangeCallback(device, callback, new IntPtr());
            if (retVal != HResultException.S_OK)
            {
                Dictionary<HResult, String> errorsMap = new Dictionary<HResult, String>() {
                    {HResultException.E_HANDLE, "The device handle specified is invalid."}
                };
                throw new HResultException(retVal, errorsMap);
            }
        }

        /// <summary>
        /// Adds a page to the specified device.
        /// </summary>
        /// <param name="device"> A handle to a device.</param>
        /// <param name="page"> A numeric identifier of a page. Usually this is the 0 based number of the page.</param>
        /// <param name="flags">If this contains DirectOutput.IsActive, then this page will become the active page. If zero,
        /// this page will not change the active page.</param>
        /// <remarks>
        /// Only one page per-application per-device should have flags contain DirectOutput.IsActive. The plugin is not informed about
        /// the active page change if the DirectOutput.IsActive is set. 
        /// </remarks>
        /// <exception cref="HResultException"></exception>
        public void AddPage(IntPtr device, Int32 page, Int32 flags)
        {
            HResult retVal = addPage(device, page, flags);
            if (retVal != HResultException.S_OK)
            {
                Dictionary<HResult, String> errorsMap = new Dictionary<HResult, String>() {
                    {HResultException.E_OUTOFMEMORY, "Insufficient memory to complete the request."},
                    {HResultException.E_INVALIDARG, "The page parameter already exists."},
                    {HResultException.E_HANDLE, "The device handle specified is invalid."}
                };
                throw new HResultException(retVal, errorsMap);
            }
        }

        /// <summary>
        /// Removes a page.
        /// </summary>
        /// <param name="device">A handle to a device.</param>
        /// <param name="page">A numeric identifier of a page. Usually this is the 0 based number of the page.</param>
        /// <exception cref="HResultException"></exception>
        public void RemovePage(IntPtr device, Int32 page)
        {
            HResult retVal = removePage(device, page);
            if (retVal != HResultException.S_OK)
            {
                Dictionary<HResult, String> errorsMap = new Dictionary<HResult, String>() {
                    {HResultException.E_INVALIDARG, "The page parameter argument does not reference a valid page id."},
                    {HResultException.E_HANDLE, "The device handle specified is invalid."}
                };
                throw new HResultException(retVal, errorsMap);
            }
        }

        /// <summary>
        /// Sets the state of a given LED indicator.
        /// </summary>
        /// <param name="device">A handle to a device.</param>
        /// <param name="page">A numeric identifier of a page. Usually this is the 0 based number of the page.</param>
        /// <param name="index">A numeric identifier of the LED. Refer to the data sheet for each device to determine what LEDs are present.</param>
        /// <param name="value">The numeric value of a given state of a LED. Refer to the data sheet for each device to determine what are legal values.</param>
        /// <remarks>
        /// value is usually 0 (off) or 1 (on).
        /// </remarks>
        /// <exception cref="HResultException"></exception>
        public void SetLed(IntPtr device, Int32 page, Int32 index, Int32 value)
        {
            HResult retVal = setLed(device, page, index, value);
            if (retVal != HResultException.S_OK)
            {
                Dictionary<HResult, String> errorsMap = new Dictionary<HResult, String>() {
                    {HResultException.E_PAGENOTACTIVE, "The specified page is not active. Displaying information is not permitted when the page is not active."},
                    {HResultException.E_INVALIDARG, "The dwPage argument does not reference a valid page id, or the dwIndex argument does not specifiy a valid LED id."},
                    {HResultException.E_HANDLE, "The device handle specified is invalid."}
                };
                throw new HResultException(retVal, errorsMap);
            }
        }

        /// <summary>
        /// Sets a string value of a given string.
        /// </summary>
        /// <param name="device">A handle to a device.</param>
        /// <param name="page">A numeric identifier of a page. Usually this is the 0 based number of the page.</param>
        /// <param name="index">A numeric identifier of the string. Refer to the data sheet for each device to determine what strings are present.</param>
        /// <param name="value">String that specifies the value to display. Providing a null pointer will clear the string.</param>
        /// <exception cref="HResultException"></exception>
        public void SetString(IntPtr device, Int32 page, Int32 index, [MarshalAsAttribute(UnmanagedType.LPWStr)] String text)
        {
            HResult retVal = setString(device, page, index, text.Length, text);
            if (retVal != HResultException.S_OK)
            {
                Dictionary<HResult, String> errorsMap = new Dictionary<HResult, String>() {
                    {HResultException.E_PAGENOTACTIVE, "The specified page is not active. Displaying information is not permitted when the page is not active."},
                    {HResultException.E_INVALIDARG, "The dwPage argument does not reference a valid page id, or the dwIndex argument does not reference a valid string id."},
                    {HResultException.E_OUTOFMEMORY, "Insufficient memory to complete the request."},
                    {HResultException.E_HANDLE, "The device handle specified is invalid."}
                };
                throw new HResultException(retVal, errorsMap);
            }
        }

        /// <summary>
        /// Sets the image data of a given image.
        /// </summary>
        /// <param name="device"> A handle to a device.</param>
        /// <param name="page">A numeric identifier of a page. Usually this is the 0 based number of the page.</param>
        /// <param name="index">A numeric identifier of the image. Refer to the data sheet for each device to determine what images are present.</param>
        /// <param name="buffer">An array of bytes that represents the raw bitmap to display on the screen.</param>
        /// <remarks>
        /// The buffer passed must be the correct size for the specified image. Devices support JPEG or BITMAP image data, and the buffer
        /// should contain all neccessary headers and footers.
        /// </remarks>
        /// <exception cref="HResultException"></exception>
        public void SetImage(IntPtr device, Int32 page, Int32 index, byte[] buffer)
        {
            HResult retVal = setImage(device, page, index, buffer.Length, buffer);
            if (retVal != HResultException.S_OK)
            {
                Dictionary<HResult, String> errorsMap = new Dictionary<HResult, String>() {
                    {HResultException.E_PAGENOTACTIVE, "The specified page is not active. Displaying information is not permitted when the page is not active."},
                    {HResultException.E_INVALIDARG, "The page argument does not reference a valid page id, or the index argument does not reference a valid image id."},
                    {HResultException.E_OUTOFMEMORY, "Insufficient memory to complete the request."},
                    {HResultException.E_HANDLE, "The device handle specified is invalid."}
                };
                throw new HResultException(retVal, errorsMap);
            }
        }
    }

}
