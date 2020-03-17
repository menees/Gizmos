namespace Menees.Gizmos.Weather
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Globalization;
	using System.Linq;
	using System.Text;
	using System.Windows;
	using System.Windows.Data;

	#endregion

	// This prevents a NotSupportedException of "ImageSourceConverter cannot convert from (null)."
	// when an Image Source is bound to a field that returns null.
	// Usage:
	// Resources: <local:NullImageConverter x:Key="nullImageConverter"/>
	// <Image Source="{Binding Path=ImagePath, Converter={StaticResource nullImageConverter}}"/>
	// From http://stackoverflow.com/questions/5399601/imagesourceconverter-error-for-source-null
	public sealed class NullImageConverter : IValueConverter
	{
		#region Public Methods

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value ?? DependencyProperty.UnsetValue;

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}

		#endregion
	}
}
