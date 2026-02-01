using QWENShared.Enums;
using System;
using System.Collections.Generic;
using System.Text;
using UnitSystem;

namespace GeminiSimulator.WashoutMatrixs
{
    public class WashoutMatrix
    {
        // La llave es: (Categoria Anterior, Categoria Siguiente)
        // El valor es un objeto interno con los dos tiempos (Mixer y Linea)
       private readonly Dictionary<(int From, int To), WashoutRule> _matrix = new();

        public void AddRule(ProductCategory from, ProductCategory to, Amount mixerTime, Amount lineTime)
        {
            // Convertimos a int inmediatamente al entrar
            int fromId = (int)from;
            int toId = (int)to;

            var key = (fromId, toId);
            var rule = new WashoutRule(mixerTime, lineTime);

            if (!_matrix.ContainsKey(key))
            {
                _matrix.Add(key, rule);
            }
            else
            {
                _matrix[key] = rule;
            }
        }

        public Amount GetMixerWashout(ProductCategory from, ProductCategory to)
        {
            // La búsqueda también se hace por int
            if (_matrix.TryGetValue(((int)from, (int)to), out var rule))
            {
                return rule.MixerTime;
            }
            return new Amount(0, UnitSystem.TimeUnits.Minute);
        }

        public Amount GetLineWashout(ProductCategory from, ProductCategory to)
        {
            if (_matrix.TryGetValue(((int)from, (int)to), out var rule))
            {
                return rule.LineTime;
            }
            return new Amount(0, UnitSystem.TimeUnits.Minute);
        }

        // Estructura interna ligera para guardar los dos valores
        private record WashoutRule(Amount MixerTime, Amount LineTime);
    }
}
