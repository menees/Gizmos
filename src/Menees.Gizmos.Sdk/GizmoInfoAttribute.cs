namespace Menees.Gizmos
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Text;

	#endregion

	/// <summary>
	/// Provides metadata about a <see cref="Gizmo"/>.
	/// </summary>
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
	public sealed class GizmoInfoAttribute : Attribute
	{
		#region Constructors

		/// <summary>
		/// Creates a new instance.
		/// </summary>
		public GizmoInfoAttribute()
		{
			this.GizmoName = string.Empty;
		}

		#endregion

		#region Public Properties

		/// <summary>
		/// Gets or sets the name to use for the associated <see cref="Gizmo"/>.
		/// </summary>
		public string GizmoName { get; set; }

		/// <summary>
		/// Gets or sets whether the associated <see cref="Gizmo"/> supports more than one instance running at a time.
		/// </summary>
		/// <remarks>
		/// If this is true, then an instance name should not be specified on the command line.
		/// If this is false, then an instance name must be specified on the command line.
		/// </remarks>
		public bool IsSingleInstance { get; set; }

		/// <summary>
		/// Gets or sets whether this is a temporary gizmo whose state shouldn't be persisted.
		/// </summary>
		public bool IsTemporary { get; set; }

		#endregion
	}
}
