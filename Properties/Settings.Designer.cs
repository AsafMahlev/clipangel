﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace ClipAngel.Properties {
    
    
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.VisualStudio.Editors.SettingsDesigner.SettingsSingleFileGenerator", "14.0.0.0")]
    internal sealed partial class Settings : global::System.Configuration.ApplicationSettingsBase {
        
        private static Settings defaultInstance = ((Settings)(global::System.Configuration.ApplicationSettingsBase.Synchronized(new Settings())));
        
        public static Settings Default {
            get {
                return defaultInstance;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("10000")]
        public int HistoryDepthNumber {
            get {
                return ((int)(this["HistoryDepthNumber"]));
            }
            set {
                this["HistoryDepthNumber"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("Alt + V")]
        public string HotkeyShow {
            get {
                return ((string)(this["HotkeyShow"]));
            }
            set {
                this["HotkeyShow"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool Autostart {
            get {
                return ((bool)(this["Autostart"]));
            }
            set {
                this["Autostart"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("1000")]
        public int MaxClipSizeKB {
            get {
                return ((int)(this["MaxClipSizeKB"]));
            }
            set {
                this["MaxClipSizeKB"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        public global::System.Collections.Specialized.StringCollection LastFilterValues {
            get {
                return ((global::System.Collections.Specialized.StringCollection)(this["LastFilterValues"]));
            }
            set {
                this["LastFilterValues"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("Default")]
        public string Language {
            get {
                return ((string)(this["Language"]));
            }
            set {
                this["Language"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool WordWrap {
            get {
                return ((bool)(this["WordWrap"]));
            }
            set {
                this["WordWrap"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool MoveCopiedClipToTop {
            get {
                return ((bool)(this["MoveCopiedClipToTop"]));
            }
            set {
                this["MoveCopiedClipToTop"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool ShowVisualWeightColumn {
            get {
                return ((bool)(this["ShowVisualWeightColumn"]));
            }
            set {
                this["ShowVisualWeightColumn"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("No")]
        public string HotkeyIncrementalPaste {
            get {
                return ((string)(this["HotkeyIncrementalPaste"]));
            }
            set {
                this["HotkeyIncrementalPaste"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool SelectTopClipOnShow {
            get {
                return ((bool)(this["SelectTopClipOnShow"]));
            }
            set {
                this["SelectTopClipOnShow"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool ClipListSimpleDraw {
            get {
                return ((bool)(this["ClipListSimpleDraw"]));
            }
            set {
                this["ClipListSimpleDraw"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool WindowAutoPosition {
            get {
                return ((bool)(this["WindowAutoPosition"]));
            }
            set {
                this["WindowAutoPosition"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool AutoCheckForUpdate {
            get {
                return ((bool)(this["AutoCheckForUpdate"]));
            }
            set {
                this["AutoCheckForUpdate"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool ClearFiltersOnClose {
            get {
                return ((bool)(this["ClearFiltersOnClose"]));
            }
            set {
                this["ClearFiltersOnClose"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool MonospacedFont {
            get {
                return ((bool)(this["MonospacedFont"]));
            }
            set {
                this["MonospacedFont"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool ShowApplicationIconColumn {
            get {
                return ((bool)(this["ShowApplicationIconColumn"]));
            }
            set {
                this["ShowApplicationIconColumn"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool ShowNativeTextFormatting {
            get {
                return ((bool)(this["ShowNativeTextFormatting"]));
            }
            set {
                this["ShowNativeTextFormatting"] = value;
            }
        }
    }
}
