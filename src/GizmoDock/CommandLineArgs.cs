namespace Menees.Gizmos
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.IO;
	using System.Linq;
	using System.Text;
	using Menees.Shell;

	#endregion

	internal sealed class CommandLineArgs
	{
		#region Constructors

		public CommandLineArgs(string[] args, IDock dock)
		{
			CommandLine parser = new(false);
			parser.AddHeader("Usage: GizmoDock /asm Assembly.dll /type GizmoType [/inst InstanceName] [/ShowInTaskbar]");

			string? assemblyName = null;
			parser.AddSwitch(
				new[] { "Assembly", "Asm" },
				"The name of the assembly.",
				(value, errors) =>
				{
					if (File.Exists(value))
					{
						assemblyName = value;
					}
					else
					{
						errors.Add("The specified assembly could not be found.");
					}
				});

			string? typeName = null;
			parser.AddSwitch(
				"Type",
				"The Gizmo-derived class to load.",
				(value, errors) => typeName = value);

			string? instanceName = null;
			parser.AddSwitch(
				"Instance",
				"The name for this Gizmo instance.",
				(value, errors) => instanceName = value);

			parser.AddSwitch(
				nameof(this.ShowInTaskbar),
				"Whether the gizmo should show in the Windows taskbar.  Defaults to false.",
				value => this.ShowInTaskbar = value);

			parser.AddSwitch(nameof(this.CloseAll), "Closes all running GizmoDock instances.  Defaults to false.", value => this.CloseAll = value);

			parser.AddFinalValidation(errors =>
			{
				if (assemblyName.IsNotEmpty() && typeName.IsNotEmpty())
				{
					this.Gizmo = Gizmo.Create(dock, assemblyName, typeName, instanceName, errors);
				}
			});

			CommandLineParseResult parseResult = parser.Parse(args);
			if (parseResult != CommandLineParseResult.Valid)
			{
				// Clean up any gizmo we've already created because we're going to replace it with a MessageGizmo.
				if (this.Gizmo is IDisposable disposable)
				{
					disposable.Dispose();
				}

				// Pass an empty array so Gizmo.Create can't add any errors to it.  It should never need to.
				MessageGizmo messageGizmo = (MessageGizmo?)Gizmo.Create(dock, typeof(MessageGizmo), null, CollectionUtility.EmptyArray<string>())
					?? throw Exceptions.NewInvalidOperationException($"Unable to create {nameof(MessageGizmo)}.");
				messageGizmo.Message = parser.CreateMessage().Trim();
				messageGizmo.IsErrorMessage = parseResult != CommandLineParseResult.HelpRequested;
				this.Gizmo = messageGizmo;
			}
			else if (this.Gizmo == null && !this.CloseAll)
			{
				// If there were no errors, no gizmo, and the command line was valid, then the command line
				// was probably empty, so we'll show the ShortcutCreator gizmo as the default.
				this.Gizmo = Gizmo.Create(dock, typeof(ShortcutCreatorGizmo), null, CollectionUtility.EmptyArray<string>());
				this.ShowInTaskbar = true;
			}
		}

		#endregion

		#region Public Properties

		public Gizmo? Gizmo { get; private set; }

		public bool ShowInTaskbar { get; private set; }

		public bool CloseAll { get; private set; }

		#endregion
	}
}
