namespace SMTAlert.Properties {
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.VisualStudio.Editors.SettingsDesigner.SettingsSingleFileGenerator", "17.0.0.0")]
    internal sealed partial class Settings : global::System.Configuration.ApplicationSettingsBase {
        private static Settings defaultInstance = ((Settings)(global::System.Configuration.ApplicationSettingsBase.Synchronized(new Settings())));
        public static Settings Default { get { return defaultInstance; } }
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string MainWindow_placement {
            get { return ((string)(this["MainWindow_placement"])); }
            set { this["MainWindow_placement"] = value; }
        }
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string IntelWindow_placement {
            get { return ((string)(this["IntelWindow_placement"])); }
            set { this["IntelWindow_placement"] = value; }
        }
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string ZKBWindow_placement {
            get { return ((string)(this["ZKBWindow_placement"])); }
            set { this["ZKBWindow_placement"] = value; }
        }
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string LastCharacterName {
            get { return ((string)(this["LastCharacterName"])); }
            set { this["LastCharacterName"] = value; }
        }
    }
}
