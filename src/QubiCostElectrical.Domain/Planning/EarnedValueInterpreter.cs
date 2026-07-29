using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

/// <summary>Interpretaciones textuales centralizadas (PMI / semáforos).</summary>
public static class EarnedValueInterpreter
{
    public static IReadOnlyList<EvmInterpretation> Interpret(EarnedValueIndicators indicators)
    {
        ArgumentNullException.ThrowIfNull(indicators);
        var list = new List<EvmInterpretation>();

        list.Add(InterpretSpi(indicators.Spi));
        list.Add(InterpretCpi(indicators.Cpi));
        list.Add(InterpretSv(indicators.Sv));
        list.Add(InterpretCv(indicators.Cv));
        list.Add(InterpretVac(indicators.Vac));
        list.Add(InterpretTcpi(indicators.Tcpi, indicators.Cpi));

        return list;
    }

    public static EvmInterpretation InterpretSpi(decimal? spi)
    {
        if (!spi.HasValue)
            return new EvmInterpretation { Indicator = "SPI", Light = EvmTrafficLight.Unknown, Message = "SPI no disponible (PV = 0)." };
        if (spi.Value < 0.9m)
            return new EvmInterpretation { Indicator = "SPI", Value = spi, Light = EvmTrafficLight.Red, Message = "Atraso significativo del cronograma." };
        if (spi.Value < 1m)
            return new EvmInterpretation { Indicator = "SPI", Value = spi, Light = EvmTrafficLight.Amber, Message = "Atraso del cronograma." };
        if (spi.Value == 1m)
            return new EvmInterpretation { Indicator = "SPI", Value = spi, Light = EvmTrafficLight.Green, Message = "Cronograma según plan." };
        return new EvmInterpretation { Indicator = "SPI", Value = spi, Light = EvmTrafficLight.Green, Message = "Proyecto adelantado." };
    }

    public static EvmInterpretation InterpretCpi(decimal? cpi)
    {
        if (!cpi.HasValue)
            return new EvmInterpretation { Indicator = "CPI", Light = EvmTrafficLight.Unknown, Message = "CPI no disponible (AC = 0)." };
        if (cpi.Value < 0.9m)
            return new EvmInterpretation { Indicator = "CPI", Value = cpi, Light = EvmTrafficLight.Red, Message = "Sobre costo significativo." };
        if (cpi.Value < 1m)
            return new EvmInterpretation { Indicator = "CPI", Value = cpi, Light = EvmTrafficLight.Amber, Message = "Sobre costo." };
        if (cpi.Value == 1m)
            return new EvmInterpretation { Indicator = "CPI", Value = cpi, Light = EvmTrafficLight.Green, Message = "Costo según presupuesto." };
        return new EvmInterpretation { Indicator = "CPI", Value = cpi, Light = EvmTrafficLight.Green, Message = "Buen desempeño económico." };
    }

    public static EvmInterpretation InterpretSv(decimal sv) =>
        sv < 0m
            ? new EvmInterpretation { Indicator = "SV", Value = sv, Light = EvmTrafficLight.Amber, Message = "Valor ganado por debajo del planificado (atraso)." }
            : sv > 0m
                ? new EvmInterpretation { Indicator = "SV", Value = sv, Light = EvmTrafficLight.Green, Message = "Valor ganado por encima del planificado (adelanto)." }
                : new EvmInterpretation { Indicator = "SV", Value = sv, Light = EvmTrafficLight.Green, Message = "Sin variación de plazo en valor." };

    public static EvmInterpretation InterpretCv(decimal cv) =>
        cv < 0m
            ? new EvmInterpretation { Indicator = "CV", Value = cv, Light = EvmTrafficLight.Amber, Message = "Costo real supera el valor ganado." }
            : cv > 0m
                ? new EvmInterpretation { Indicator = "CV", Value = cv, Light = EvmTrafficLight.Green, Message = "Costo real inferior al valor ganado." }
                : new EvmInterpretation { Indicator = "CV", Value = cv, Light = EvmTrafficLight.Green, Message = "Sin variación de costo." };

    public static EvmInterpretation InterpretVac(decimal vac) =>
        vac < 0m
            ? new EvmInterpretation { Indicator = "VAC", Value = vac, Light = EvmTrafficLight.Red, Message = "Se prevé terminar por encima del BAC." }
            : vac > 0m
                ? new EvmInterpretation { Indicator = "VAC", Value = vac, Light = EvmTrafficLight.Green, Message = "Se prevé terminar bajo el BAC." }
                : new EvmInterpretation { Indicator = "VAC", Value = vac, Light = EvmTrafficLight.Green, Message = "Se prevé terminar exactamente en BAC." };

    public static EvmInterpretation InterpretTcpi(decimal? tcpi, decimal? cpi)
    {
        if (!tcpi.HasValue)
            return new EvmInterpretation { Indicator = "TCPI", Light = EvmTrafficLight.Unknown, Message = "TCPI no disponible." };
        if (tcpi.Value > 1.1m)
            return new EvmInterpretation { Indicator = "TCPI", Value = tcpi, Light = EvmTrafficLight.Red, Message = "El rendimiento futuro requerido es exigente (difícil de alcanzar)." };
        if (cpi.HasValue && tcpi.Value > cpi.Value)
            return new EvmInterpretation { Indicator = "TCPI", Value = tcpi, Light = EvmTrafficLight.Amber, Message = "TCPI superior al CPI actual: se requiere mejorar el desempeño." };
        return new EvmInterpretation { Indicator = "TCPI", Value = tcpi, Light = EvmTrafficLight.Green, Message = "TCPI alcanzable respecto del desempeño actual." };
    }
}
