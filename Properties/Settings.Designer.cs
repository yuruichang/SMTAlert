namespace SMTAlert.Properties
{
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.VisualStudio.Editors.SettingsDesigner.SettingsSingleFileGenerator", "17.0.0.0")]
    internal sealed partial class Settings : global::System.Configuration.ApplicationSettingsBase
    {
        private static Settings defaultInstance = ((Settings)(global::System.Configuration.ApplicationSettingsBase.Synchronized(new Settings())));

        public static Settings Default
        {
            get { return defaultInstance; }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string MainWindow_placement
        {
            get { return ((string)(this["MainWindow_placement"])); }
            set { this["MainWindow_placement"] = value; }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string OverlayWindow_placement
        {
            get { return ((string)(this["OverlayWindow_placement"])); }
            set { this["OverlayWindow_placement"] = value; }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string ZKBMonitorWindow_placement
        {
            get { return ((string)(this["ZKBMonitorWindow_placement"])); }
            set { this["ZKBMonitorWindow_placement"] = value; }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string ZKB_ColumnWidths
        {
            get { return ((string)(this["ZKB_ColumnWidths"])); }
            set { this["ZKB_ColumnWidths"] = value; }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool OverlayWindow_Open
        {
            get { return ((bool)(this["OverlayWindow_Open"])); }
            set { this["OverlayWindow_Open"] = value; }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string OverlayWindow_CharacterName
        {
            get { return ((string)(this["OverlayWindow_CharacterName"])); }
            set { this["OverlayWindow_CharacterName"] = value; }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool ZKBMonitorWindow_Open
        {
            get { return ((bool)(this["ZKBMonitorWindow_Open"])); }
            set { this["ZKBMonitorWindow_Open"] = value; }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool AlertChannelWindow_Open
        {
            get { return ((bool)(this["AlertChannelWindow_Open"])); }
            set { this["AlertChannelWindow_Open"] = value; }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string AlertChannelWindow_placement
        {
            get { return ((string)(this["AlertChannelWindow_placement"])); }
            set { this["AlertChannelWindow_placement"] = value; }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool MainWindow_MinimizedToTray
        {
            get { return ((bool)(this["MainWindow_MinimizedToTray"])); }
            set { this["MainWindow_MinimizedToTray"] = value; }
        }
    }
}
