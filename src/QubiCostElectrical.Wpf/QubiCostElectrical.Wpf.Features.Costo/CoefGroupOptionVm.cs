#nullable enable
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Costo;

public sealed class CoefGroupOptionVm : ViewModelBase
{
	private bool isAvailable = true;

	public string Code { get; }

	public bool IsAvailable
	{
		get
		{
			return isAvailable;
		}
		set
		{
			SetProperty(ref isAvailable, value, "IsAvailable");
		}
	}

	public CoefGroupOptionVm(string code)
	{
		Code = code;
	}

	public override string ToString()
	{
		return Code;
	}
}
