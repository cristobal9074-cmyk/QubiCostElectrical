#nullable enable
using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Features.About;

public class AboutDialog : Window, IComponentConnector
{
	internal TextBlock VersionText = null!;

	internal TextBlock LicenseText = null!;

	internal TextBlock TechnicalText = null!;

	internal TextBlock DataFolderText = null!;

	private bool _contentLoaded;

	public AboutDialog(ILicenseService? license = null)
	{
		InitializeComponent();
		VersionText.Text = "Qubi Cost Electrical Â· versiÃ³n " + ProductVersionInfo.ProductVersionLabel;
		TechnicalText.Text = ProductVersionInfo.TechnicalSummary;
		DataFolderText.Text = ProductPaths.GetLocalAppDataRoot();
		LicenseText.Text = DescribeLicense(license);
	}

	private static string DescribeLicense(ILicenseService? license)
	{
		if (license == null)
		{
			return "Sin activar (modo de demostraciÃ³n).";
		}
		switch (license.Status)
		{
		case LicenseStatus.Valid:
		{
			LicenseInfo? currentLicense = license.CurrentLicense;
			if ((object?)currentLicense != null)
			{
				return $"VÃ¡lida Â· {currentLicense.Company} Â· vence el {currentLicense.ExpiresAt:d}.";
			}
			break;
		}
		case LicenseStatus.GracePeriod:
			return "En perÃ­odo de gracia Â· se acerca la fecha de expiraciÃ³n.";
		case LicenseStatus.Expired:
			return "Expirada Â· la aplicaciÃ³n quedÃ³ en modo de solo lectura.";
		case LicenseStatus.Invalid:
			return "InvÃ¡lida Â· la firma de la licencia no pudo verificarse.";
		}
		return "Sin activar (modo de demostraciÃ³n).";
	}

	private void OnCloseClick(object sender, RoutedEventArgs e)
	{
		base.DialogResult = true;
		Close();
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "10.0.9.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/QubiCostElectrical;V1.0.0.0;component/features/about/aboutdialog.xaml", UriKind.Relative);
			Application.LoadComponent(this, resourceLocator);
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "10.0.9.0")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	void IComponentConnector.Connect(int connectionId, object target)
	{
		switch (connectionId)
		{
		case 1:
			VersionText = (TextBlock)target;
			break;
		case 2:
			LicenseText = (TextBlock)target;
			break;
		case 3:
			TechnicalText = (TextBlock)target;
			break;
		case 4:
			DataFolderText = (TextBlock)target;
			break;
		case 5:
			((Button)target).Click += OnCloseClick;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
