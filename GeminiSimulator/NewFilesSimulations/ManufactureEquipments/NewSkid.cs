using GeminiSimulator.Materials;
using GeminiSimulator.NewFilesSimulations.BaseClasss;
using GeminiSimulator.NewFilesSimulations.Operators;
using GeminiSimulator.NewFilesSimulations.Tanks;
using QWENShared.Enums;
using UnitSystem;

namespace GeminiSimulator.NewFilesSimulations.ManufactureEquipments
{


    public class NewSkid : NewManufacture
    {
        public double Flow = 0;

        // 1. LA COLA AUTÓNOMA DEL SKID
        private Queue<NewRecipedInletTank> _skidQueue = new Queue<NewRecipedInletTank>();

        public NewWipTank? CurrentWip;

        // Propiedades para que el Estado Available pueda ver la cola
        public bool HasPendingOrders => _skidQueue.Count > 0;

        public NewSkid(Guid id, string name, ProcessEquipmentType type, FocusFactory focusFactory, Amount nominalFlow)
            : base(id, name, type, focusFactory)
        {
            Flow = nominalFlow.GetValue(MassFlowUnits.Kg_sg);
        }

        // --- 2. CICLO DE VIDA E INICIALIZACIÓN ---
        public override void CheckInitialStatus(DateTime initialDate)
        {
            base.CheckInitialStatus(initialDate);
            // IMPORTANTE: Arrancamos con el estado INTELIGENTE, no el genérico.
            TransitionOutletState(new NewSkidAvailableState(this));
        }

        // --- 3. RECEPCIÓN DE ÓRDENES (Lógica FIFO) ---
        public override void ReceiveStartCommand(NewRecipedInletTank wipTank)
        {
            // Si no estoy en Available (estoy produciendo o esperando recursos), a la cola.
            if (OutletState is not NewSkidAvailableState)
            {
                _skidQueue.Enqueue(wipTank);
                return;
            }

            // Si estoy libre, intento arrancar de una vez.
            CurrentMaterial = wipTank.CurrentMaterial;
            ExecuteStartLogic(wipTank);
        }

        // --- 4. MOTOR DE ARRANQUE (ExecuteStartLogic) ---
        protected override void ExecuteStartLogic(NewRecipedInletTank wipTank)
        {
            if (GlobalState.IsOperational)
            {
                // A. Intentamos capturar el WIP (Destino)
                if (wipTank.RequestAccess(this))
                {
                    // B. Intentamos capturar las bombas (Origen)
                    if (TryReserveResources(wipTank.CurrentMaterial))
                    {
                        // ¡ÉXITO TOTAL! -> A Producir
                        CurrentWip = wipTank as NewWipTank;
                        TransitionOutletState(new NewSkidProducingOutlet(this));
                    }
                    else
                    {
                        // FALLO PARCIAL: Tengo el tanque, pero no las bombas.
                        // Me quedo con el tanque reservado y espero las bombas.
                        CurrentWip = wipTank as NewWipTank;
                        TransitionOutletState(new NewSkidWaitingForResourcesState(this));
                    }
                }
                else
                {
                    // FALLO TOTAL: El tanque destino está ocupado por otro.
                    // Lo devuelvo a la cola para intentarlo después.
                    _skidQueue.Enqueue(wipTank);

                    // Si no estoy haciendo nada más, vuelvo a Available
                    // (El estado Available se encargará de reintentar la cola)
                    if (OutletState is not NewSkidProducingOutlet && OutletState is not NewSkidWaitingForResourcesState)
                    {
                        TransitionOutletState(new NewSkidAvailableState(this));
                    }
                }
            }
        }

        // --- 5. PARADA Y RELEVO (Handshake Continuo) ---
        public override void ReceiveStopCommand()
        {
            // A. Soltamos Todo
            ReleaseResources();

            // B. Intentamos procesar el siguiente INMEDIATAMENTE
            TryProcessNextInQueue();
        }

        // Helper para intentar sacar de la cola (Usado por ReceiveStop y por AvailableState)
        public void TryProcessNextInQueue()
        {
            if (_skidQueue.TryDequeue(out var nextWip))
            {
                // ¡SÍ! Continuidad inmediata.
                CurrentMaterial = nextWip.CurrentMaterial;
                ExecuteStartLogic(nextWip);
            }
            else
            {
                // Nadie en cola. A descansar.
                TransitionOutletState(new NewSkidAvailableState(this));
            }
        }

        private void ReleaseResources()
        {
            // Soltar Bombas
            foreach (var pump in InletPumps)
            {
                if (pump.CurrentOwner == this)
                {
                    pump.ReleaseAccess(this);
                    pump.SetCurrentFlow(0);
                }
            }
            // Soltar WIP
            if (CurrentWip != null)
            {
                CurrentWip.ReleaseAccess(this);
                CurrentWip.SetInletFlow(0);
                CurrentWip = null;
            }
        }

        // --- 6. GESTIÓN DE RECURSOS ---
        public bool TryReserveResources(ProductDefinition? product)
        {
            if (product?.RecipeSteps == null) return false;
            var reservedPumps = new List<NewPump>();
            bool allSuccess = true;

            foreach (var step in product.RecipeSteps)
            {
                var matchingPump = InletPumps.FirstOrDefault(p =>
                    p.SupportedProducts.Any(x => x.Id == step.IngredientId));

                if (matchingPump != null)
                {
                    if (matchingPump.RequestAccess(this))
                    {
                        reservedPumps.Add(matchingPump);
                        var pumpflow = step.TargetPercentage / 100.0 * Flow;
                        matchingPump.SetCurrentFlow(pumpflow);
                    }
                    else
                    {
                        allSuccess = false;
                        break;
                    }
                }
            }

            if (allSuccess) return true;

            // Rollback si falló alguna bomba
            foreach (var pump in reservedPumps)
            {
                pump.ReleaseAccess(this);
                pump.SetCurrentFlow(0);
            }
            return false;
        }

        // --- 7. REPORTES ---
        protected override void AddSpecificReportData(List<NewInstantaneousReport> reportList)
        {
            string neutral = "#212121"; // Negro
            string activeColor = "#673AB7"; // Morado

            double currentFlow = (OutletState is NewSkidProducingOutlet ? Flow : 0) * 3600;

            reportList.Add(new NewInstantaneousReport("Current Flow", $"{currentFlow:F0} Kg/hr", IsBold: true, Color: activeColor));
            reportList.Add(new NewInstantaneousReport("Wip Target", CurrentWip?.Name ?? "None", Color: neutral));

            if (_skidQueue.Count > 0)
            {
                reportList.Add(new NewInstantaneousReport("Queue", $"{_skidQueue.Count} Orders Waiting", IsBold: true, Color: "#E91E63"));
            }
        }
    }

    // =========================================================
    // ESTADOS ESPECÍFICOS DEL SKID
    // =========================================================

    // 1. ESTADO DISPONIBLE INTELIGENTE (Polling)
    public class NewSkidAvailableState : INewOutletState
    {
        private readonly NewSkid _skid;
        public NewSkidAvailableState(NewSkid skid) => _skid = skid;

        public bool IsProductive => true; // Disponible para producir
        public string StateLabel => "Skid Available";
        public string SubStateName => string.Empty;
        public string HexColor => "#00C853"; // Verde

        public void Calculate()
        {
            // AQUÍ ESTÁ LA MAGIA:
            // Si el Skid está "disponible" pero tiene órdenes pendientes (porque rebotaron),
            // intenta procesarlas en cada tick hasta que logre enganchar una.
            if (_skid.HasPendingOrders)
            {
                _skid.TryProcessNextInQueue();
            }
        }

        public void CheckTransitions() { }
    }

    // 2. ESTADO PRODUCIENDO
    public class NewSkidProducingOutlet : INewOutletState
    {
        public string SubStateName => string.Empty;
        private readonly NewSkid _skid;
        public NewSkidProducingOutlet(NewSkid skid) => _skid = skid;

        public bool IsProductive => true;
        public string StateLabel => $"Producing -> {_skid.CurrentWip?.Name}";
        public string HexColor => "#00C853"; // Verde

        public void Calculate()
        {
            // Lógica de flujo continuo
            if (_skid.CurrentWip != null)
            {
                _skid.CurrentWip.SetInletFlow(_skid.Flow);
            }
        }

        public void CheckTransitions() { }
    }

    // 3. ESTADO ESPERANDO BOMBAS (Resource Starvation)
    public class NewSkidWaitingForResourcesState : INewOutletState
    {
        private readonly NewSkid _skid;
        public string SubStateName => string.Empty;
        public NewSkidWaitingForResourcesState(NewSkid skid) => _skid = skid;

        public bool IsProductive => false;
        public string StateLabel => "Waiting for Pumps";
        public string HexColor => "#FF9800"; // Naranja

        public void Calculate()
        {
            // Solo esperamos
        }

        public void CheckTransitions()
        {
            // Ya tengo el WIP capturado, solo insisto con las bombas
            if (_skid.CurrentWip != null)
            {
                if (_skid.TryReserveResources(_skid.CurrentWip.CurrentMaterial))
                {
                    _skid.TransitionOutletState(new NewSkidProducingOutlet(_skid));
                }
            }
            else
            {
                // Error de consistencia, volver a inicio
                _skid.TransitionOutletState(new NewSkidAvailableState(_skid));
            }
        }
    }
}
