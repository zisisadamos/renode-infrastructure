//
// Copyright (c) 2021 Zisis Adamos
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//

using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Time;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class STM32F4_Watchdog : BasicDoubleWordPeripheral, IKnownSize
    {
        //TODO: Could propably get osc_frequency from RCC.
        //TODO: Stop timer on debug stop.
        //TODO: Use RCC to set restart cause.
        public STM32F4_Watchdog(Machine machine, long osc_frequency) : base(machine)
        {
            watchdogTimer = new LimitTimer(machine.ClockSource, osc_frequency, this, "STM32_IWDG", watchdogDefaultRLRValue, workMode: WorkMode.OneShot, enabled: false, eventEnabled: false);
            watchdogTimer.LimitReached += TimerLimitReachedCallback;
            DefineRegisters();
        }

        public override void Reset()
        {
            watchdogTimer.Reset();
        }

        public long Size => 0x400;
        private void DefineRegisters()
        {
            Register.IWDG_KR.Define(this)
            .WithValueField(0, 16, writeCallback: (_, value) =>
            {
                switch(value)
                {
                    case valueToResetWatchdog:
                        watchdogTimer.Limit = watchdogRLRValue;
                        registersUnlocked = false;
                        break;
                    case valueToStartWatchdog:
                        watchdogTimer.EventEnabled = true;
                        watchdogTimer.Enabled = true;
                        registersUnlocked = false;
                        break;
                    case valueToUnlockRegisters:
                        registersUnlocked = true;
                        break;
                    default:
                        registersUnlocked = false;
                        break;
                }
            }, name: "KEY")
            .WithReservedBits(16, 16);

            Register.IWDG_PR.Define(this)
            .WithValueField(0, 3, writeCallback: (_, value) =>
            {
                watchdogTimer.Divider = (int)Math.Pow(2, (2 + value));
            }, name: "PR")
            .WithReservedBits(3, 29);

            Register.IWDG_RLR.Define(this)
           .WithValueField(0, 12, writeCallback: (_, value) =>
           {
               if(registersUnlocked)
               {
                   watchdogRLRValue = value;
               }
               else
               {
                   this.Log(LogLevel.Warning, "Trying to change watchdog reload value without unlocking it");
               }
           }, name: "RL")
           .WithReservedBits(12, 20);

            Register.IWDG_SR.Define(this)
            .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => registersUnlocked, name: "PVU")
            .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => registersUnlocked, name: "RVU")
            .WithReservedBits(2, 30);
        }

        private void TimerLimitReachedCallback()
        {
            this.Log(LogLevel.Warning, "Watchdog reset triggered!");
            machine.RequestReset();
        }


        private readonly LimitTimer watchdogTimer;
        private bool registersUnlocked = false;
        private const uint valueToResetWatchdog = 0xAAAA;
        private const uint valueToStartWatchdog = 0xCCCC;
        private const uint valueToUnlockRegisters = 0x5555;
        private const uint watchdogDefaultRLRValue = 0xFFF;
        private const uint watchdogDefaultPrescalerValue = 4;
        private uint watchdogRLRValue = watchdogDefaultRLRValue;

        private enum Register
        {
            IWDG_KR = 0x0,
            IWDG_PR = 0x4,
            IWDG_RLR = 0x8,
            IWDG_SR = 0xC
        }
    }
}
