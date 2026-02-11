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

        public NewSkid(Guid id, string name, ProcessEquipmentType type, FocusFactory focusFactory, Amount nominalFlow)
            : base(id, name, type, focusFactory)
        {
            Flow = nominalFlow.GetValue(MassFlowUnits.Kg_sg);
        }

        // --- 1. LÓGICA DE RECURSOS (Impecable, no la tocamos) ---
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

            foreach (var pump in reservedPumps)
            {
                pump.ReleaseAccess(this);
                pump.SetCurrentFlow(0);
            }
            return false;
        }

        // --- 2. CAMBIO CRÍTICO: Usamos ExecuteStartLogic ---
        // NO sobreescribimos ReceiveStartCommand. Dejamos que la BASE decida si encola o arranca.
        // Nosotros solo definimos CÓMO arrancar cuando nos den luz verde.
        public NewWipTank? CurrentWip;
        protected override void ExecuteStartLogic(NewRecipedInletTank wipTank)
        {
            // Nota: NewManufacture ya asignó CurrentMaterial = wipTank.CurrentMaterial antes de llamarnos

            if (GlobalState.IsOperational)
            {
                // Intentamos capturar el WIP (el destino)
                if (wipTank.RequestAccess(this))
                {

                    // Intentamos capturar las bombas (el origen)
                    if (TryReserveResources(wipTank.CurrentMaterial))
                    {
                        CurrentWip = wipTank as NewWipTank;
                        // ÉXITO TOTAL: A producir
                        TransitionOutletState(new NewSkidProducingOutlet(this));
                    }
                    else
                    {
                        // FALLO PARCIAL: Tengo el tanque, pero no las bombas. A esperar.
                        TransitionOutletState(new NewSkidWaitingForResourcesState(this));
                    }
                }
                else
                {
                    // RARO: No me dieron el tanque WIP. 
                    // Lo devolvemos a la cola para intentar luego.
                    _wipTankQueue.Enqueue(wipTank);
                }
            }
        }

        // --- 3. CAMBIO CRÍTICO: Revisar la cola al parar ---
        public override void ReceiveStopCommand()
        {
            // A. Soltamos Bombas
            foreach (var pump in InletPumps)
            {
                // Solo soltamos las que son nuestras (por si acaso)
                if (pump.CurrentOwner == this)
                {
                    pump.ReleaseAccess(this);
                    pump.SetCurrentFlow(0);

                }
            }

            // B. Soltamos el Tanque WIP
            if (CurrentWip != null)
            {
                CurrentWip?.ReleaseAccess(this);
                CurrentWip?.SetInletFlow(0);
                CurrentWip=null;    
            }

            // C. ¡IMPORTANTE! Preguntamos a la base si hay otro en la fila
            CheckQueueAndStartNext();
        }

        // CheckInitialStatus: CORRECTO. Solo llamamos a la base.
        // La base (NewManufacture) ya se encarga de llenar la lista InletPumps.
        public override void CheckInitialStatus(DateTime initialDate)
        {
            base.CheckInitialStatus(initialDate);
        }
        protected override void AddSpecificReportData(List<NewInstantaneousReport> reportList)
        {
            double currentFlow = (OutletState is NewSkidProducingOutlet ? Flow : 0 )* 3600;

            reportList.Add(new NewInstantaneousReport("Current Flow", $"{currentFlow:F0}, Kg/hr", IsBold: true, Color: "#673AB7")); // Morado

            reportList.Add(new NewInstantaneousReport("Wip", CurrentWip?.Name ?? string.Empty, IsBold: false, Color: "#673AB7")); // Morado
            reportList.Add(new NewInstantaneousReport("Material", CurrentWip?.CurrentMaterial?.Name ?? string.Empty, IsBold: false, Color: "#673AB7")); // Morado


            // --- 1. BLOQUE DE PRODUCTO (Contexto del Lote) ---
           
        }
    }
    public class NewSkidProducingOutlet : INewOutletState
    {
        public string SubStateName => string.Empty;
        private readonly NewSkid _skid;
        public NewSkidProducingOutlet(NewSkid skid) => _skid = skid;

        public bool IsProductive => true;
        public string StateLabel => $"Producing -> {_skid.CurrentWip?.Name}";
        public string HexColor => "#00C853";

        public void Calculate()
        {




            // 2. ENVIAR AL WIP
            if (_skid.CurrentWip != null)
            {
                _skid.CurrentWip.SetInletFlow(_skid.Flow);
            }
        }

        public void CheckTransitions()
        {
            // Solo lógica de negocio (¿Ya terminé la orden? ¿Me pararon manualmente?)
            // La lógica de "Bomba falló" ya la maneja papá NewPlantUnit.
        }
    }
    public class NewSkidWaitingForResourcesState : INewOutletState
    {
        private readonly NewSkid _skid;
        public string SubStateName => string.Empty;
        public NewSkidWaitingForResourcesState(NewSkid skid) => _skid = skid;

        public bool IsProductive => false; // No produce, está esperando
        public string StateLabel => "Waiting for Pumps";
        public string HexColor => "#FF9800"; // Ámbar/Naranja (Estándar para "Wait")

        public void Calculate()
        {
            // Aquí no hacemos nada físico, solo esperamos.
        }

        public void CheckTransitions()
        {
            // En cada ciclo, intentamos reservar de nuevo.
            // El wipTank ya es nuestro (lo capturamos en ReceiveStartCommand), 
            // así que solo necesitamos las bombas.

            // Obtenemos el material del WIP que tenemos capturado
            if (_skid.CurrentWip != null)
            {
                // Intentamos reservar recursos para el material que el WIP ya tiene
                if (_skid.TryReserveResources(_skid.CurrentWip.CurrentMaterial))
                {
                    // ¡Éxito! Transicionamos a producción
                    _skid.TransitionOutletState(new NewSkidProducingOutlet(_skid));
                }
            }
            else
            {
                // Si por algún error CurrentWip es nulo, volvemos al estado disponible
                _skid.TransitionOutletState(new NewManufactureAvailableState(_skid));
            }
        }
    }
}
