using System;
using MonoDevelop.Core;
using MonoDevelop.Ide.Gui.Dialogs;
using MonoDevelop.D.Building;
using MonoDevelop.Ide;
using Gtk;
using MonoDevelop.D.Building.CompilerPresets;
using System.IO;
namespace MonoDevelop.D.OptionPanels
{
	/// <summary>
	/// This panel provides UI access to project independent D settings such as generic compiler configurations, library and import paths etc.
	/// </summary>
	public partial class DCompilerOptions : Bin
	{
		#region Properties & Init
		private ListStore compilerStore = new ListStore (typeof(string), typeof(DCompilerConfiguration));
		private DCompilerConfiguration configuration;
		string defaultCompilerVendor;
		private BuildArgumentOptions releaseArgumentsDialog = null;
		private BuildArgumentOptions debugArgumentsDialog = null;

		public DCompilerOptions ()
		{
			this.Build ();

			var textRenderer = new CellRendererText ();
			
			cmbCompilers.Clear ();

			cmbCompilers.PackStart (textRenderer, false);
			cmbCompilers.AddAttribute (textRenderer, "text", 0);

			cmbCompilers.Model = compilerStore;

			releaseArgumentsDialog = new BuildArgumentOptions ();
			debugArgumentsDialog = new BuildArgumentOptions ();
		}
		#endregion

		#region Preset management
		public void ReloadCompilerList ()
		{
			compilerStore.Clear ();

			defaultCompilerVendor = DCompilerService.Instance.DefaultCompiler;

			foreach (var cmp in DCompilerService.Instance.Compilers) {
				var virtCopy = new DCompilerConfiguration ();
				virtCopy.AssignFrom (cmp);
				var iter = compilerStore.AppendValues (cmp.Vendor, virtCopy);
				if (cmp.Vendor == defaultCompilerVendor)
					cmbCompilers.SetActiveIter(iter);
			}
		}

		string ComboBox_CompilersLabel {
			get {
				return cmbCompilers.ActiveText;
			}
		}
		
		protected void OnCmbCompilersChanged (object sender, EventArgs e)
		{
			TreeIter iter;
			if (cmbCompilers.GetActiveIter (out iter)) {
				var newConfig = cmbCompilers.Model.GetValue (iter, 1) as DCompilerConfiguration;

				if (configuration == newConfig)
					return;
				else
					ApplyToVirtConfiguration ();

				Load (newConfig);
			} else if (!compilerStore.GetIterFirst (out iter)) {
				ApplyToVirtConfiguration ();
				Load (null);
			}
		}

		void MakeCurrentConfigDefault ()
		{
			if (configuration != null) {
				defaultCompilerVendor = configuration.Vendor;
				btnMakeDefault.Active = true;
			}
		}

		protected void OnTogglebuttonMakeDefaultPressed (object sender, EventArgs e)
		{
			if (configuration != null && configuration.Vendor == defaultCompilerVendor)
				btnMakeDefault.Active = true;
			else
				MakeCurrentConfigDefault ();
		}
		#endregion

		#region Save&Load
		public void Load (DCompilerConfiguration compiler)
		{
			configuration = compiler;

			if (compiler == null) {
				txtBinPath.Text =
					txtCompiler.Text =
					txtConsoleAppLinker.Text =
					txtSharedLibLinker.Text =
					txtStaticLibLinker.Text = null;

				text_DefaultLibraries.Buffer.Clear ();
				text_Includes.Buffer.Clear ();

				releaseArgumentsDialog.Load (null, false);
				debugArgumentsDialog.Load (null, true);

				btnMakeDefault.Sensitive = false;
				return;
			}
			//for now, using Executable target compiler command for all targets source compiling
			LinkTargetConfiguration targetConfig;
			targetConfig = compiler.GetOrCreateTargetConfiguration (DCompileTarget.Executable);
			
			txtBinPath.Text = compiler.BinPath;
			txtCompiler.Text = compiler.SourceCompilerCommand;
			check_enableLibPrefixing.Active = compiler.EnableGDCLibPrefixing;
			
			//linker targets 			
			targetConfig = compiler.GetOrCreateTargetConfiguration (DCompileTarget.Executable); 						
			txtConsoleAppLinker.Text = targetConfig.Linker;			
			
			targetConfig = compiler.GetOrCreateTargetConfiguration (DCompileTarget.SharedLibrary); 						
			txtSharedLibLinker.Text = targetConfig.Linker;
			
			targetConfig = compiler.GetOrCreateTargetConfiguration (DCompileTarget.StaticLibrary); 						
			txtStaticLibLinker.Text = targetConfig.Linker;

			releaseArgumentsDialog.Load (compiler, false);		
			debugArgumentsDialog.Load (compiler, true);				

			text_DefaultLibraries.Buffer.Text = string.Join (Environment.NewLine, compiler.DefaultLibraries);
			text_Includes.Buffer.Text = string.Join (Environment.NewLine, compiler.IncludePaths);

			btnMakeDefault.Active = 
				configuration.Vendor == defaultCompilerVendor;
			btnMakeDefault.Sensitive = true;

			using (var buf = new StringWriter ())
			using (var xml = new System.Xml.XmlTextWriter (buf)) {
				xml.Formatting = System.Xml.Formatting.Indented;
				xml.WriteStartDocument();
				xml.WriteStartElement("patterns");
				compiler.ArgumentPatterns.SaveTo (xml);
				xml.WriteEndDocument();
				tb_ArgPatterns.Buffer.Text = buf.ToString ();
			}
		}

		public bool Validate ()
		{
			return true;
		}

		public bool Store ()
		{
			if (!ApplyToVirtConfiguration())
				return false;

			DCompilerService.Instance.Compilers.Clear ();

			TreeIter iter;
			compilerStore.GetIterFirst (out iter);
			do {
				var virtCmp = compilerStore.GetValue (iter, 1) as DCompilerConfiguration;
				
				DCompilerService.Instance.Compilers.Add (virtCmp);
			} while (compilerStore.IterNext(ref iter));

			DCompilerService.Instance.DefaultCompiler = defaultCompilerVendor;

			return true;
		}

		public bool ApplyToVirtConfiguration ()
		{
			if (configuration == null)
				return false;
			
			configuration.BinPath = txtBinPath.Text;
			configuration.SourceCompilerCommand = txtCompiler.Text;
			configuration.EnableGDCLibPrefixing = check_enableLibPrefixing.Active;

			var targetConfig = configuration.GetOrCreateTargetConfiguration (DCompileTarget.Executable); 			
			targetConfig.Linker = txtConsoleAppLinker.Text;
			
			targetConfig = configuration.GetOrCreateTargetConfiguration (DCompileTarget.SharedLibrary); 						
			targetConfig.Linker = txtSharedLibLinker.Text;
			
			targetConfig = configuration.GetOrCreateTargetConfiguration (DCompileTarget.StaticLibrary); 						
			targetConfig.Linker = txtStaticLibLinker.Text;
			
			releaseArgumentsDialog.Store ();			
			debugArgumentsDialog.Store ();					
			
			configuration.DefaultLibraries.Clear ();
			foreach (var p in text_DefaultLibraries.Buffer.Text.Split('\n'))
			{
				var p_ = p.Trim();
				if (!String.IsNullOrWhiteSpace(p_))
					configuration.DefaultLibraries.Add(p_);
			}

			try
			{
				using (var sr = new StringReader(tb_ArgPatterns.Buffer.Text))
				using (var xml = new System.Xml.XmlTextReader(sr))
					configuration.ArgumentPatterns.ReadFrom(xml);
			}
			catch (Exception ex) { 
				LoggingService.LogError("Error during parsing argument patterns", ex); 
				return false; 
			}
			
			#region Store new include paths
			configuration.IncludePaths.Clear();
			foreach (var p in text_Includes.Buffer.Text.Split('\n'))
			{
				var p_ = p.Trim().TrimEnd('\\', '/');
				if (!string.IsNullOrWhiteSpace(p_))
					configuration.IncludePaths.Add(p_);
			}

			try {
				// Update parse cache immediately
				configuration.UpdateParseCacheAsync();
			} catch (Exception ex) {
				LoggingService.LogError ("Include path analysis error", ex);
			}
			#endregion

			return true;
		}
		#endregion

		#region Setting edititing helper methods
		private void ShowArgumentsDialog (bool isDebug)
		{
			BuildArgumentOptions dialog = null;
			if (isDebug)
				dialog = debugArgumentsDialog;
			else
				dialog = releaseArgumentsDialog;

			MessageService.RunCustomDialog (dialog, IdeApp.Workbench.RootWindow);
		}
		
		protected void btnReleaseArguments_Clicked (object sender, EventArgs e)
		{			
			ShowArgumentsDialog (false);						
		}

		protected void btnDebugArguments_Clicked (object sender, EventArgs e)
		{
			ShowArgumentsDialog (true);			
		}

		string lastDir;
		protected void OnButtonAddIncludeClicked (object sender, EventArgs e)
		{
			FileChooserDialog dialog = new FileChooserDialog (
				"Select D Source Folder",
				IdeApp.Workbench.RootWindow,
				FileChooserAction.SelectFolder,
				"Cancel",
				ResponseType.Cancel,
				"Ok",
				ResponseType.Ok) 
			{ 
				TransientFor=Toplevel as Window,
				WindowPosition = WindowPosition.Center
			};

			if (lastDir != null)
				dialog.SetCurrentFolder(lastDir);
			else if (Directory.Exists(txtBinPath.Text))
				dialog.SetCurrentFolder(txtBinPath.Text);

			try {
				if (dialog.Run() == (int)ResponseType.Ok)
				{
					lastDir = dialog.Filename;
					text_Includes.Buffer.Text += (text_Includes.Buffer.CharCount == 0 ? "" : "\n") + dialog.Filename;
				}
			} finally {
				dialog.Destroy ();
			}
		}

		protected void OnButtonBinPathBrowserClicked (object sender, EventArgs e)
		{
			var dialog = new FileChooserDialog ("Select Compiler's bin path", null, FileChooserAction.SelectFolder, "Cancel", ResponseType.Cancel, "Ok", ResponseType.Ok)
			{
				TransientFor = Toplevel as Window,
				WindowPosition = WindowPosition.Center
			};

			try {
				if (dialog.Run () == (int)ResponseType.Ok)
					txtBinPath.Text = dialog.Filename;
			} finally {
				dialog.Destroy ();
			}
		}

		protected void OnBtnDefaultsClicked (object sender, EventArgs e)
		{
			if (configuration == null)
				return;

			if (!PresetLoader.HasPresetsAvailable (configuration)) {
				MessageService.ShowMessage ("No defaults available for " + configuration.Vendor);
				return;
			}

			if (MessageService.AskQuestion ("Reset current compiler preset?", AlertButton.Yes, AlertButton.No) == AlertButton.Yes && 
				PresetLoader.TryLoadPresets (configuration))
				Load (configuration);
		}
		#endregion
	}
	
	public class DCompilerOptionsBinding : OptionsPanel
	{
		private DCompilerOptions panel;
		
		public override Widget CreatePanelWidget ()
		{
			panel = new DCompilerOptions ();
			LoadConfigData ();
			return panel;
		}
		
		public void LoadConfigData ()
		{
			panel.ReloadCompilerList ();
		}

		bool hasStoredAlready = false;
		public override bool ValidateChanges ()
		{
			return panel.Validate() && panel.Store() && (hasStoredAlready = true);
		}
			
		public override void ApplyChanges ()
		{
			if (!hasStoredAlready)
				panel.Store();
			hasStoredAlready = false; // Acceptable here
		}
	}	
}
