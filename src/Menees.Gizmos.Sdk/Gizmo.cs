namespace Menees.Gizmos
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.Diagnostics;
	using System.Diagnostics.CodeAnalysis;
	using System.IO;
	using System.Linq;
	using System.Reflection;
	using System.Security;
	using System.Text;
	using System.Windows;
	using Menees.Windows.Presentation;

	#endregion

	/// <summary>
	/// The base class for a gizmo.
	/// </summary>
	public class Gizmo : ExtendedUserControl
	{
		#region Private Data Members

		private GizmoInfo gizmoInfo;

		#endregion

		#region Public Properties

		/// <summary>
		/// Gets the dock that is hosting this gizmo.
		/// </summary>
		public IDock Dock { get; private set; }

		/// <summary>
		/// Gets the instance name associated with this gizmo.
		/// </summary>
		/// <remarks>
		/// For single-instance gizmos, this can be null.  For multi-instance gizmos
		/// this is used to save unique settings per instance (including window position).
		/// </remarks>
		public string InstanceName { get; private set; }

		/// <summary>
		/// Gets the cached metadata about this gizmo.
		/// </summary>
		public GizmoInfo Info
		{
			get
			{
				if (this.gizmoInfo == null)
				{
					this.gizmoInfo = new GizmoInfo(this.GetType());
				}

				return this.gizmoInfo;
			}
		}

		#endregion

		#region Public Methods

		/// <summary>
		/// Creates a gizmo using the specified assembly and type names.
		/// </summary>
		/// <param name="dock">The dock that's hosting the gizmo.</param>
		/// <param name="assemblyName">The name of the assembly to load.</param>
		/// <param name="typeName">The name of the type to load from the specified assembly.</param>
		/// <param name="instanceName">The instance name to use.</param>
		/// <param name="errors">A collection to add error message to if necessary.</param>
		/// <returns>A new gizmo instance if it was created successfully.
		/// Null if errors were added to the <paramref name="errors"/> collection.</returns>
		public static Gizmo Create(IDock dock, string assemblyName, string typeName, string instanceName, IList<string> errors)
		{
			Conditions.RequireReference(dock, () => dock);
			Conditions.RequireString(typeName, () => typeName);

			Gizmo result = null;

			Assembly assembly = LoadAssembly(assemblyName, errors);
			if (assembly != null)
			{
				Type type = assembly.GetType(typeName, false);
				if (type == null)
				{
					// If they didn't give us a fully-qualified name, look for matching class names.
					var types = assembly.ExportedTypes.Where(t => t.Name == typeName);
					switch (types.Count())
					{
						case 0:
							errors.Add("Unable to find a type named " + typeName + " in the specified assembly.");
							break;

						case 1:
							type = types.First();
							break;

						default:
							errors.Add(
								"More than one type named " + typeName + " exists in the specified assembly.  Please use a namespace-qualified type name.");
							break;
					}
				}

				if (type != null)
				{
					result = Create(dock, type, instanceName, errors);
				}
			}

			return result;
		}

		/// <summary>
		/// Creates a gizmo using the specified type.
		/// </summary>
		/// <param name="dock">The dock that's hosting the gizmo.</param>
		/// <param name="type">A Gizmo-derived type.</param>
		/// <param name="instanceName">The instance name to use.</param>
		/// <param name="errors">A collection to add error message to if necessary.</param>
		/// <returns>A new gizmo instance if it was created successfully.
		/// Null if errors were added to the <paramref name="errors"/> collection.</returns>
		public static Gizmo Create(IDock dock, Type type, string instanceName, IList<string> errors)
		{
			Conditions.RequireReference(dock, () => dock);
			Conditions.RequireReference(type, () => type);

			Gizmo result = null;

			if (!type.IsSubclassOf(typeof(Gizmo)))
			{
				errors.Add("Type " + type.FullName + " does not derive from " + typeof(Gizmo).FullName);
			}
			else
			{
				GizmoInfo info = new GizmoInfo(type);
				if (info.IsSingleInstance && !string.IsNullOrEmpty(instanceName))
				{
					errors.Add(info.GizmoName + " is a single instance gizmo, so an instance name isn't supported.");
				}
				else if (!info.IsSingleInstance && string.IsNullOrEmpty(instanceName))
				{
					errors.Add(info.GizmoName + " is a multi-instance gizmo, so an instance name is required.");
				}
				else
				{
					try
					{
						result = (Gizmo)Activator.CreateInstance(type);
						result.gizmoInfo = info;
						result.Dock = dock;
						result.InstanceName = instanceName;
					}
					catch (MissingMethodException ex)
					{
						errors.Add(ex.Message);
					}
					catch (TargetInvocationException ex)
					{
						errors.Add(ex.GetBaseException().Message);
					}
				}
			}

			return result;
		}

		/// <summary>
		/// Gets the metadata for all of the gizmo types exported from the specified assembly.
		/// </summary>
		/// <param name="assemblyName">The name of the assembly to load.</param>
		/// <param name="errors">A collection to add error message to if necessary.</param>
		/// <returns>A new list of GizmoInfos.</returns>
		public static IList<GizmoInfo> GetGizmoTypes(string assemblyName, IList<string> errors)
		{
			IList<GizmoInfo> result = null;

			Assembly assembly = LoadAssembly(assemblyName, errors);
			if (assembly != null)
			{
				result = assembly.ExportedTypes
					.Where(t => t.IsSubclassOf(typeof(Gizmo)))
					.Select(t => new GizmoInfo(t))
					.Where(g => !g.IsTemporary)
					.OrderBy(g => g.GizmoName)
					.ToList();
			}

			return result;
		}

		/// <summary>
		/// Creates a new <see cref="OptionsPage"/> if <see cref="GizmoInfo.HasOptions"/> is true.
		/// </summary>
		/// <returns>The base implementation always returns null.  Derived classes should override <see cref="OnCreateOptionsPage"/>.</returns>
		public OptionsPage CreateOptionsPage()
		{
			if (!this.Info.HasOptions)
			{
				throw Exceptions.NewArgumentException(this.Info.GizmoName + " does not have options.");
			}

			return this.OnCreateOptionsPage();
		}

		/// <summary>
		/// Called when the gizmo should load its settings.
		/// </summary>
		/// <param name="settings">A gizmo-specific node to load settings from.</param>
		public void LoadSettings(ISettingsNode settings)
		{
			this.OnLoadSettings(settings);
		}

		/// <summary>
		/// Called when the gizmo should save its settings.
		/// </summary>
		/// <param name="settings">A gizmo-specfic node to save settings to.</param>
		public void SaveSettings(ISettingsNode settings)
		{
			this.OnSaveSettings(settings);
		}

		/// <summary>
		/// Gets the gizmo's loggable properties, typically passed to a <see cref="Log"/> method.
		/// </summary>
		/// <returns>A new dictionary with the gizmo's important properties to log.</returns>
		[SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate", Justification = "Creates a new dictionary on each call.")]
		public IDictionary<string, object> GetLogProperties()
		{
			Dictionary<string, object> result = new Dictionary<string, object>();
			result.Add(nameof(Gizmo), this.Info.GizmoName);
			if (!this.Info.IsSingleInstance)
			{
				result.Add("Instance", this.InstanceName);
			}

			return result;
		}

		/// <summary>
		/// Closes the gizmo and its host window.
		/// </summary>
		public void Close()
		{
			Window root = Window.GetWindow(this);
			if (root != null)
			{
				root.Close();
			}
		}

		/// <summary>
		/// Allows a gizmo to be dragged by a mouse with its left button down.
		/// </summary>
		public void DragGizmo()
		{
			Window root = Window.GetWindow(this);
			if (root != null)
			{
				root.DragMove();
			}
		}

		#endregion

		#region Protected Methods

		/// <summary>
		/// Creates a new <see cref="OptionsPage"/> if <see cref="GizmoInfo.HasOptions"/> is true.
		/// </summary>
		/// <returns>The base implementation always returns null.  Derived classes should override <see cref="OnCreateOptionsPage"/>.</returns>
		protected internal virtual OptionsPage OnCreateOptionsPage() => null;

		/// <summary>
		/// Called when the gizmo should load its settings.
		/// </summary>
		/// <param name="settings">A gizmo-specific node to load settings from.</param>
		protected virtual void OnLoadSettings(ISettingsNode settings)
		{
		}

		/// <summary>
		/// Called when the gizmo should save its settings.
		/// </summary>
		/// <param name="settings">A gizmo-specfic node to save settings to.</param>
		protected virtual void OnSaveSettings(ISettingsNode settings)
		{
		}

		#endregion

		#region Private Methods

		private static Assembly LoadAssembly(string assemblyName, IList<string> errors)
		{
			Conditions.RequireString(assemblyName, () => assemblyName);
			Conditions.RequireReference(errors, () => errors);

			Assembly assembly = null;
			Exception exception = null;
			try
			{
				AssemblyName name = AssemblyName.GetAssemblyName(assemblyName);

				// See if they asked for an assembly that matches one that's already in the "Load" context.
				// The Load context assemblies are preferable.  If a user of the "Gizmo Shortcut Creator"
				// selects one of the core assemblies used by GizmoDock but they select it from a different
				// directory, then we need to use the already loaded assembly.  Otherwise, later checks for
				// type.IsSubclassOf(typeof(Gizmo)) could fail because typeof(Gizmo) would exist as different
				// types in both the Load and LoadFrom contexts.
				Assembly[] loadContextAssemblies = AppDomain.CurrentDomain.GetAssemblies();
				assembly = loadContextAssemblies.FirstOrDefault(asm => asm.FullName == name.FullName);
				if (assembly == null)
				{
					// Because we're using the LoadFrom context, we have to use the AssemblyResolve event
					// to treat the Load and LoadFrom assemblies as the same.  We use -= and += to ensure
					// that we only register a single handler even if LoadAssembly is called multiple times.
					AppDomain.CurrentDomain.AssemblyResolve -= CurrentDomain_AssemblyResolve;
					AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
					assembly = Assembly.LoadFrom(assemblyName);
				}
			}
			catch (IOException ex)
			{
				exception = ex;
			}
			catch (BadImageFormatException ex)
			{
				exception = ex;
			}
			catch (SecurityException ex)
			{
				exception = ex;
			}
			catch (ArgumentException ex)
			{
				exception = ex;
			}

			if (assembly == null)
			{
				errors.Add("The assembly \"" + assemblyName + "\" could not be loaded.");
			}

			if (exception != null)
			{
				errors.Add(exception.Message);
				Dictionary<string, object> properties = new Dictionary<string, object>();
				properties.Add("AssemblyName", assemblyName);
				Log.Error(typeof(Gizmo), "Error loading assembly.", exception, properties);
			}

			return assembly;
		}

		#endregion

		#region Private Event Handlers

		private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
		{
			Assembly result = null;

			// We load the Gizmo assembly into the LoadFrom context, but if it needs to use types from
			// the Load context, we have to handle the assembly resolution manually.  .NET won't look
			// in other load contexts automatically.
			string assemblyName = args.Name;
			Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
			foreach (Assembly assembly in assemblies)
			{
				if (assembly.FullName == assemblyName)
				{
					result = assembly;
					break;
				}
			}

			return result;
		}

		#endregion
	}
}
