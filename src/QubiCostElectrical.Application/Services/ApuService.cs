using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

public sealed class ApuService : IApuService
{
    private readonly ApuEngine engine = new();

    public ApuRow CrearDesdeItem(ItemizadoRow item) => engine.CreateFromItem(item);

    public List<string> Validar(ApuRow apu) => engine.Validate(apu).ToList();
}
