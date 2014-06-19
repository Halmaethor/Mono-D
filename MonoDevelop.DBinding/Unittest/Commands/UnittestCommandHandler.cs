//
// UnittestCommandHandler.cs
//
// Author:
//       Foerdi
//
// https://github.com/aBothe/Mono-D/pull/334
//
// Copyright (c) 2013

using MonoDevelop.Components.Commands;
using MonoDevelop.Ide;
using MonoDevelop.Projects;
using MonoDevelop.D.Projects;

namespace MonoDevelop.D.Unittest.Commands
{//TODO: Implement RunSingle/RunSingleExternally
	public class UnittestCommandHandler : CommandHandler
	{
		CommandInfo commandInfo;
		protected override void Update (CommandInfo info)
		{
			commandInfo = info;
			base.Update (info);
		}
		protected override void Run (object dataItem)
		{
			MessageHandler guiRun = delegate
			{
				var project = IdeApp.ProjectOperations.CurrentSelectedProject as DProject;
				if(project == null)
					return;
				
				DProjectConfiguration conf = project.Configurations["Unittest"] as DProjectConfiguration;
				if(conf == null)
					return;
				
				ProjectFile file = IdeApp.ProjectOperations.CurrentSelectedItem as ProjectFile;
				if(file == null)
					return;
					
				string filePath = file.FilePath.FullPath;
				
				IdeApp.Workbench.SaveAll();
				
				if((string)commandInfo.Command.Id ==  "MonoDevelop.D.Unittest.Commands.UnittestCommands.RunExternal")
					UnittestCore.RunExternal(filePath,project,conf);
				else
					UnittestCore.Run(filePath,project,conf);
			};
			DispatchService.GuiDispatch(guiRun);
		}
	}

	class UnittestCmdHdlrFromEditor : CommandHandler
	{
		protected override void Update (CommandInfo info)
		{
			info.Visible = info.Enabled = 
				IdeApp.Workbench.ActiveDocument.HasProject && IdeApp.Workbench.ActiveDocument.Project is AbstractDProject;
		}

		protected override void Run (object dataItem)
		{
			var prj = IdeApp.Workbench.ActiveDocument.Project as AbstractDProject;
			if (prj == null)
				return;

			// If having a dub project, run dub test <package>

			// otherwise, let it compile with unittest args etc. and run it
		}
	}
}

