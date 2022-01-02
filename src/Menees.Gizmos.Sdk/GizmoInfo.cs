namespace Menees.Gizmos
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Reflection;
	using System.Text;

	#endregion

	/// <summary>
	/// Caches the metadata for a <see cref="Gizmo"/>.
	/// </summary>
	public sealed class GizmoInfo
	{
		#region Constructors

		/// <summary>
		/// Creates an instance for the specified gizmo type.
		/// </summary>
		/// <param name="gizmoType">A type derived from <see cref="Gizmo"/>.</param>
		public GizmoInfo(Type gizmoType)
		{
			Conditions.RequireReference(gizmoType, nameof(gizmoType));
			Conditions.RequireArgument(gizmoType.IsSubclassOf(typeof(Gizmo)), "gizmoType must derive from Gizmo.", nameof(gizmoType));

			this.GizmoType = gizmoType;

			// Use the info specified at compile time if it's available.
			GizmoInfoAttribute? attribute = gizmoType.GetCustomAttribute<GizmoInfoAttribute>(false);
			if (attribute != null)
			{
				this.GizmoName = attribute.GizmoName;
				this.IsSingleInstance = attribute.IsSingleInstance;
				this.IsTemporary = attribute.IsTemporary;
			}

			// See if the type overrides the OnCreateOptionsPage method.
			const string MethodName = nameof(Gizmo.OnCreateOptionsPage);
			MethodInfo? method = gizmoType.GetMethod(MethodName, BindingFlags.DeclaredOnly | BindingFlags.NonPublic | BindingFlags.Instance);
			this.HasOptions = method != null;

			// Make sure the gizmo name isn't crap.
			if (this.GizmoName.IsBlank())
			{
				this.GizmoName = gizmoType.Name;
			}
		}

		#endregion

		#region Public Properties

		/// <summary>
		/// Gets the name to use for the associated <see cref="Gizmo"/>.
		/// </summary>
		public string GizmoName { get; }

		/// <summary>
		/// Gets the <see cref="Gizmo"/>-derived type.
		/// </summary>
		public Type GizmoType { get; }

		/// <summary>
		/// Gets whether the associated <see cref="Gizmo"/> supports options.
		/// </summary>
		/// <remarks>
		/// If this is true, then GizmoDock will display an Options button and the gizmo's
		/// <see cref="Gizmo.CreateOptionsPage"/> will be called when the Options button
		/// is clicked.
		/// </remarks>
		public bool HasOptions { get; }

		/// <summary>
		/// Gets whether the associated <see cref="Gizmo"/> supports more than one instance running at a time.
		/// </summary>
		/// <remarks>
		/// If this is true, then an instance name should not be specified on the command line.
		/// If this is false, then an instance name must be specified on the command line.
		/// </remarks>
		public bool IsSingleInstance { get; }

		/// <summary>
		/// Gets whether this is a temporary gizmo whose state shouldn't be persisted.
		/// </summary>
		public bool IsTemporary { get; }

		#endregion
	}
}
