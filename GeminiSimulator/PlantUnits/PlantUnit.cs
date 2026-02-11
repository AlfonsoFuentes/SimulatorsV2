using GeminiSimulator.DesignPatterns;
using GeminiSimulator.Helpers;
using GeminiSimulator.Materials;
using GeminiSimulator.Plans;
using QWENShared.DTOS.BaseEquipments;
using QWENShared.Enums;
using System;
using System.Collections.Generic;
using System.Text;
using UnitSystem;

namespace GeminiSimulator.PlantUnits
{

    public abstract partial class PlantUnit
    {
        public override string ToString()
        {
            return Name;
        }
        // --- IDENTIDAD ---
        public Guid Id { get; private set; }
        public string Name { get; private set; }
        public ProcessEquipmentType Type { get; private set; }
        public FocusFactory FocusFactory { get; private set; }

        // --- CONECTIVIDAD (Topología) ---
        // 1. Temporales (Ids)
        private List<Guid> _inputIds = new();
        private List<Guid> _outputIds = new();

        // 2. Reales (Objetos conectados)
        public List<PlantUnit> Inputs { get; private set; } = new();
        public List<PlantUnit> Outputs { get; private set; } = new();

        // --- CAPACIDAD ---
        // Almacena la capacidad ya procesada (Kg o Kg/s) según lo que le diga el Loader.
        protected Dictionary<ProductDefinition, Amount> _productCapabilities = new();

        public Dictionary<ProductDefinition, Amount> ProductCapabilities => _productCapabilities;
        public List<ProductDefinition> Materials => _productCapabilities.Select(x => x.Key).ToList();

        // --- DISPONIBILIDAD ---
        private List<PlannedDownTimeWindow> _scheduledBreaks = new();

        public SimulationContext? Context { get; private set; }

        public void SetContext(SimulationContext _Context) => Context = _Context;
        protected PlantUnit( Guid id, string name, ProcessEquipmentType type, FocusFactory focusFactory)
        {
            Id = id;
            Name = name;
            Type = type;
            FocusFactory = focusFactory;
            
        }

        // --- MÉTODOS DE CONFIGURACIÓN (Para uso exclusivo del Loader) ---

        public void AddInlet(Guid fromId)
        {
            if (!_inputIds.Contains(fromId)) _inputIds.Add(fromId);
        }

        public void AddOutlet(Guid toId)
        {
            if (!_outputIds.Contains(toId)) _outputIds.Add(toId);
        }

        public void AddPlannedDownTime(TimeSpan start, TimeSpan end)
        {
            _scheduledBreaks.Add(new PlannedDownTimeWindow(start, end));
        }

        /// <summary>
        /// Registra la capacidad para un material.
        /// El Loader ya debe haber convertido el valor a la unidad correcta (Kg o Flujo).
        /// </summary>
        public void SetProductCapability(ProductDefinition materialId, Amount capacity)
        {
            if (!_productCapabilities.ContainsKey(materialId))
            {
                _productCapabilities.Add(materialId, capacity);
            }
        }
        // --- CABLEADO (WIRE-UP) ---
        // Convierte los IDs en Objetos Reales al final de la carga
        public void WireUp(Dictionary<Guid, PlantUnit> allUnits)
        {
            Inputs.Clear();
            Outputs.Clear();

            foreach (var id in _inputIds)
            {
                if (allUnits.TryGetValue(id, out var unit)) Inputs.Add(unit);
            }

            foreach (var id in _outputIds)
            {
                if (allUnits.TryGetValue(id, out var unit)) Outputs.Add(unit);
            }
        }

        // --- LÓGICA DE NEGOCIO (Kardex) ---

        public bool CanProcess(ProductDefinition product)
        {
            if (product == null || _productCapabilities.Count == 0) return false;
            return _productCapabilities.ContainsKey(product);
        }

        // Virtual: Los hijos pueden cambiar la lógica si quieren.
        public virtual Amount GetCapacity(ProductDefinition? product = null)
        {
            // Lógica por defecto: Buscar en diccionario si pasan producto
            if (product != null && _productCapabilities.TryGetValue(product, out var cap))
            {
                return cap;
            }
            return new Amount(0, UnitMeasure.None);
        }

        public bool IsOnPlannedBreak(DateTime currentTime)
        {
            TimeOnly time = TimeOnly.FromDateTime(currentTime);
            foreach (var window in _scheduledBreaks)
            {
                if (window.IsInside(time)) return true;
            }
            return false;
        }
        // --- CICLO DE VIDA DE LA SIMULACIÓN (Template Methods) ---



    }
}
