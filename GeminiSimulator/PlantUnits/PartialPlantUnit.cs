using GeminiSimulator.DesignPatterns;
using GeminiSimulator.Main;
using System;
using System.Collections.Generic;
using System.Text;

namespace GeminiSimulator.PlantUnits
{
    public abstract partial class PlantUnit :  IInitializationStrategy, ICalculationStratgey, IReportable
    {
       
        public virtual Dictionary<string, ReportField> GetReportData()
        {
            return new Dictionary<string, ReportField>
        {
            // El nombre siempre en negrita y más grande
            { "Name", new ReportField(Name, IsBold: true, TextSize: "1.2rem") },


        };
        }

        public double TotalSeconds { get; set; } 
        public virtual void InitialNotify()
        { }
        public virtual void Notify()
        { }
        public virtual void InitialUpdate(){}
        public virtual void Update() { }
      
        public virtual void CheckInitialStatus(DateTime InitialDate) { }

        public void PrepareUnit(DateTime InitialDate)
        {
          
            CheckInitialStatus(InitialDate);
            InitialNotify();
        }
        protected IUnitState? _inboundState = null!;

        // Estado de lo que sale del equipo
        protected IUnitState? _outboundState = null!;

        public IUnitState? InboundState => _inboundState;
        public IUnitState? OutboundState => _outboundState;

        // Métodos para transitar de forma independiente

        public DateTime CurrentDate { get; private set; }
        public void Calculate(DateTime currentTime)
        {
            CurrentDate = currentTime;
            TotalSeconds++;
            ExecuteProcess();
            CheckStatus();
            Notify();

            

        }
        public void ExecuteProcess()
        {
            _outboundState?.Calculate();
            _inboundState?.Calculate();
        }

        public void CheckStatus()
        {
            _outboundState?.CheckTransitions();
            _inboundState?.CheckTransitions();
        }

        protected void TransitionInBoundInternal(IUnitState newState)
        {
            if (newState == null) return;
            _inboundState = newState;
           
        }

        protected void TransitionOutboundInternal(IUnitState newState)
        {
            if (newState == null) return;
            _outboundState = newState;
           
        }

    }
}
