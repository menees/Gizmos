namespace Menees.Gizmos.Weather
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Text;

	#endregion

	public sealed class Settings
	{
		#region Internal Fields

		internal static readonly Settings Default = new Settings(true);

		#endregion

		#region Constructors

		public Settings()
		{
			// Default this to the values I want.
			this.UserLocation = "37115";
			this.UseFahrenheit = true;
		}

		public Settings(string location, bool useFahrenheit)
		{
			this.UserLocation = location;
			this.UseFahrenheit = useFahrenheit;
		}

		private Settings(bool isReadOnly)
			: this()
		{
			this.IsReadOnly = isReadOnly;
		}

		#endregion

		#region Public Properties

		public string UserLocation { get; private set; }

		public bool UseFahrenheit { get; private set; }

		public bool IsReadOnly { get; }

		#endregion

		#region Public Methods

		public void Load(ISettingsNode settings)
		{
			this.RequireWritable();
			this.UserLocation = settings.GetValue(nameof(this.UserLocation), this.UserLocation);
			this.UseFahrenheit = settings.GetValue(nameof(this.UseFahrenheit), this.UseFahrenheit);
		}

		public void Save(ISettingsNode settings)
		{
			this.RequireWritable();
			settings.SetValue(nameof(this.UserLocation), this.UserLocation);
			settings.SetValue(nameof(this.UseFahrenheit), this.UseFahrenheit);
		}

		#endregion

		#region Private Methods

		private void RequireWritable()
		{
			Conditions.RequireState(!this.IsReadOnly, "The settings must be writable.");
		}

		#endregion
	}
}
