using GeminiSimulator.DesignPatterns;
using GeminiSimulator.Materials;
using GeminiSimulator.PlantUnits.PumpsAndFeeder.Operators;
using GeminiSimulator.PlantUnits.PumpsAndFeeder.Pumps;
using GeminiSimulator.PlantUnits.Tanks;
using QWENShared.Enums;
using UnitSystem;

namespace GeminiSimulator.PlantUnits.ManufacturingEquipments.Skids
{
    public class ContinuousSystem : EquipmentManufacture
    {
        public override Dictionary<string, ReportField> GetReportData()
        {
            var data = base.GetReportData(); // Trae el "Name"

            // 1. Caso: Línea no programada
            data.Add("Inlet State", new ReportField(_inboundState?.StateName ?? string.Empty));
            data.Add("Outlet State", new ReportField(_outboundState?.StateName ?? string.Empty));
            data.Add("Current Flow", new ReportField($"{CurrentFlowRate:F2} Kg/s"));
            data.Add("Material", new ReportField(CurrentMaterial?.Name ?? string.Empty));



            return data;
        }
        protected ProductDefinition? CurrentMaterial;
        public double NominalFlowRate { get; private set; }
        public ContinuousWipTank? CurrentWipTank;

        // Mantenemos la lista para iterar rápido
        private List<Pump> _sessionPumps = new();
        // NUEVO: Diccionario para saber el % de cada bomba
        private Dictionary<Pump, double> _pumpRatios = new();

        private List<Operator> _seseionOperator = new();
        public double CurrentFlowRate { get; private set; }

        public ContinuousSystem(Guid id, string name, ProccesEquipmentType type, FocusFactory factory, Amount nominalFlow)
            : base(id, name, type, factory)
        {
            NominalFlowRate = nominalFlow.GetValue(MassFlowUnits.Kg_sg);
        }

        public void ReceiveRequirementFromWIP(ContinuousWipTank wip)
        {
            if (wip == null) return;
            CurrentWipTank = wip;
            CurrentMaterial = CurrentWipTank.CurrentMaterial;

            // Limpieza inicial
            _sessionPumps.Clear();
            _pumpRatios.Clear();
            _seseionOperator = Inputs.OfType<Operator>().ToList();

            // LÓGICA DE EMPAREJAMIENTO (MATCHING) RECETA <-> BOMBAS
            var connectedPumps = Inputs.OfType<Pump>().ToList();

            if (CurrentMaterial?.Recipe != null)
            {
                foreach (var step in CurrentMaterial.Recipe.Steps)
                {
                    // Buscamos la bomba que puede manejar el ingrediente de este paso
                    var matchingPump = connectedPumps.FirstOrDefault(p => p.Materials.Any(x=>x.Id==step.IngredientId));
                    // Nota: Asumo que Pump tiene CanProcess o similar. 
                    // Si usas Materials.Any(id), reemplázalo aquí:
                    // .FirstOrDefault(p => p.Materials.Any(m => m.Id == step.IngredientId));

                    if (matchingPump != null)
                    {
                        if (!_sessionPumps.Contains(matchingPump))
                            _sessionPumps.Add(matchingPump);

                        // Guardamos el ratio (Ej: 10% = 0.10)
                        // Asumimos que step.Percentage viene en base 100 (ej. 50.0 para 50%)
                        _pumpRatios[matchingPump] = step.TargetPercentage / 100.0;
                    }
                }
            }
        }
        public void InletPumpsConsuming()
        {
            // Calculamos cuánto deben bombear las bombas para sostener mi flujo nominal
            foreach (var pump in _sessionPumps)
            {
                if (_pumpRatios.TryGetValue(pump, out double ratio))
                {
                    // Cálculo: Si el Skid va a trabajar a su Nominal, la bomba necesita X
                    double requiredFlow = NominalFlowRate * ratio;

                    // Ordenamos a la bomba
                    pump.SetCommandedFlow(requiredFlow, this);
                }
            }
        }

        public void InletPumpsNotConsuming()
        {
            // Ordenamos a todas las bombas que se detengan (Flujo 0)
            foreach (var pump in _sessionPumps)
            {
                pump.SetCommandedFlow(0, this);
            }
        }
        public override void Update()
        {
            // ... (Tu lógica de Update es correcta y se mantiene igual) ...
            if (CurrentWipTank == null) return;

            if (CurrentWipTank.InboundState is ReceiveingFromSkid)
            {
                ManageProductionResources();
                return;
            }

            ReleaseResources();

            if (CurrentWipTank.InboundState is WaitingLevelToStart)
            {
                if (!(_inboundState is SkidInletStarved))
                {
                    TransitionInBound(new SkidInletStarved(this));
                    TransitionOutbound(new SkidOutletStarved(this));
                }
            }
        }

        private void ManageProductionResources()
        {
            // 1. PEDIR TURNO
            foreach (var pump in _sessionPumps) pump.RequestAccess(this);

            // 2. VERIFICAR PERMISOS
            bool resourcesGranted = _sessionPumps.All(p => p.IsOwnedBy(this));
            if (!resourcesGranted)
            {
                if (!(_inboundState is SkidWaitingForResources))
                {
                    TransitionInBound(new SkidWaitingForResources(this));
                    TransitionOutbound(new SkidOutletStarved(this)); // Output Starved llama a NotProduce()
                }
                return;
            }

            // 3. VERIFICAR SALUD
            bool pumpsHealthy = !_sessionPumps.Any(x => x.OutboundState is PumpOutletNotAvailable);
            if (!pumpsHealthy)
            {
                if (!(_inboundState is SkidStoppedByInlet))
                {
                    TransitionInBound(new SkidStoppedByInlet(this));
                    TransitionOutbound(new SkidOutletStarved(this));
                }
            }
            else
            {
                if (!(_inboundState is SkidConsumingRecipe))
                {
                    TransitionInBound(new SkidConsumingRecipe(this));
                    TransitionOutbound(new SkidOutletProducing(this));
                }
            }
        }

        private void ReleaseResources()
        {
            foreach (var pump in _sessionPumps)
            {
                // Importante: Antes de soltar, ponemos el flujo a 0
                pump.SetCommandedFlow(0, this);
                pump.ReleaseAccess(this);
            }
        }

        // --- ACCIONES FÍSICAS (AQUÍ ESTÁ EL CAMBIO CLAVE) ---

        public void Produce()
        {
            // 1. Mi flujo de salida hacia el WIP
            CurrentFlowRate = NominalFlowRate;

            CurrentWipTank?.SetInletFlow(CurrentFlowRate);
            // 2. Comandar a las bombas esclavas
            foreach (var pump in _sessionPumps)
            {
                if (_pumpRatios.TryGetValue(pump, out double ratio))
                {
                    // Cálculo: Si el Skid va a 5 Kg/s y la receta pide 10%, la bomba va a 0.5 Kg/s
                    double requiredFlow = CurrentFlowRate * ratio;
                    pump.SetCommandedFlow(requiredFlow, this);
                }
            }
        }

        public void NotProduce()
        {
            CurrentFlowRate = 0;
            CurrentWipTank?.SetInletFlow(CurrentFlowRate);
            // Ordenar a las bombas detenerse
            foreach (var pump in _sessionPumps)
            {
                pump.SetCommandedFlow(0, this);
            }
        }

        public override void Notify() => CurrentWipTank?.Update();

        public void DetachWip(ContinuousWipTank wip)
        {
            if (CurrentWipTank == wip)
            {
                ReleaseResources();
                CurrentWipTank?.SetInletFlow(0);
                CurrentWipTank = null;
                CurrentMaterial = null;
                TransitionInBound(new SkidNotScheduled(this));
                TransitionOutbound(new SkidOutletStarved(this));
            }
        }

        // ... (CheckInitialStatus y las clases de Estado se mantienen igual) ...

        // Helpers
        public void TransitionInBound(SkidInletState newState) => TransitionInBoundInternal(newState);
        public void TransitionOutbound(SkidOutletState newState) => TransitionOutboundInternal(newState);
    }
    public abstract class SkidInletState : IUnitState {
        public virtual string SubStateName => string.Empty;
        protected ContinuousSystem _skid = null!; public abstract string StateName { get; } public SkidInletState(ContinuousSystem skid) => _skid = skid; public virtual void Calculate() { } public virtual void CheckTransitions() { } }
    public class SkidNotScheduled : SkidInletState { public SkidNotScheduled(ContinuousSystem skid) : base(skid) { } public override string StateName => $"Skid {_skid.Name} not shceduled"; }
    public class SkidNotMaterialRecipe : SkidInletState { public SkidNotMaterialRecipe(ContinuousSystem skid) : base(skid) { } public override string StateName => $"Skid {_skid.Name} not material recipe"; }
    public class SkidInletStarved : SkidInletState { public SkidInletStarved(ContinuousSystem skid) : base(skid) { } public override string StateName => $"Skid {_skid.Name} starved"; }
    public class SkidConsumingRecipe : SkidInletState { public SkidConsumingRecipe(ContinuousSystem skid) : base(skid) { } public override string StateName => $"Skid {_skid.Name} consuming"; public override void Calculate() { _skid.InletPumpsConsuming(); } }
    public class SkidStoppedByInlet : SkidInletState { public SkidStoppedByInlet(ContinuousSystem skid) : base(skid) { } public override string StateName => $"Skid {_skid.Name} stoped By Inlet"; public override void Calculate() { _skid.InletPumpsNotConsuming(); } }
    public class SkidWaitingForResources : SkidInletState { public SkidWaitingForResources(ContinuousSystem skid) : base(skid) { } public override string StateName => "Queued / Waiting for Pump"; public override void Calculate() { _skid.NotProduce(); } }

    public abstract class SkidOutletState : IUnitState {
        public virtual string SubStateName => string.Empty;
        protected ContinuousSystem _skid = null!; public abstract string StateName { get; } public SkidOutletState(ContinuousSystem skid) => _skid = skid; public virtual void Calculate() { } public virtual void CheckTransitions() { } }
    public class SkidOutletProducing : SkidOutletState { public SkidOutletProducing(ContinuousSystem skid) : base(skid) { } public override string StateName => $"{_skid.Name} Producing"; public override void Calculate() { _skid.Produce(); } }
    public class SkidOutletStarved : SkidOutletState { public SkidOutletStarved(ContinuousSystem skid) : base(skid) { } public override string StateName => $"{_skid.Name} Starved"; public override void Calculate() { _skid.NotProduce(); } }
    public class SkidOutletNoAbleToProdcue : SkidOutletState { public SkidOutletNoAbleToProdcue(ContinuousSystem skid) : base(skid) { } public override string StateName => $"{_skid.Name} no able to produce"; }
}
