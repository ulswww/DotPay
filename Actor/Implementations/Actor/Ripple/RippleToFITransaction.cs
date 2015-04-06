﻿
using System;
using System.Threading.Tasks;
using Dotpay.Actor.Events;
using Dotpay.Common;
using Dotpay.Common.Enum;
using Orleans;
using Orleans.EventSourcing;
using Orleans.Providers;

namespace Dotpay.Actor.Implementations
{
    [StorageProvider(ProviderName = Constants.StorageProviderName)]
    public class RippleToFI : EventSourcingGrain<RippleToFI, IRippleToFITransactionState>, IRippleToFITransaction
    {
        #region IRippleToFITransaction
        Task IRippleToFITransaction.Initialize(string rippleTxId, string invoiceId, TransferToFITargetInfo transferTargetInfo, decimal amount, decimal sendAmount, string memo)
        {
            if (string.IsNullOrEmpty(this.State.InvoiceId))
                return this.ApplyEvent(new RippleToFIInitializedEvent(this.GetPrimaryKeyLong(), rippleTxId, invoiceId, transferTargetInfo, amount, sendAmount, memo));

            return TaskDone.Done;
        }

        async Task<ErrorCode> IRippleToFITransaction.Lock(Guid managerId)
        {
            if (this.State.Locked && this.State.OperatorId != managerId)
            {
                return ErrorCode.RippleToFiIsLockedByOther;
            }
            else
            {
                await this.ApplyEvent(new RippleToFILockedEvent(managerId));
                return ErrorCode.None;
            }
        }

        async Task<ErrorCode> IRippleToFITransaction.Complete(string transferNo, Guid managerId, string managerMemo)
        {
            //如果被标记为失败的，允许管理员直接再次进行完成操作
            if (this.State.Status == RippleToFITransactionStatus.LockeByProcessor ||
                this.State.Status == RippleToFITransactionStatus.Failed)
            {
                if (this.State.Locked && this.State.OperatorId == managerId)
                {
                    await this.ApplyEvent(new RippleToFICompletedEvent(transferNo, managerId, managerMemo));
                    return ErrorCode.None;
                }
                else if (this.State.Locked && this.State.OperatorId != managerId)
                {
                    return ErrorCode.RippleToFiIsLockedByOther;
                }
            }
            return ErrorCode.None;
        }

        async Task<ErrorCode> IRippleToFITransaction.Fail(string reason, Guid managerId)
        {
            if (this.State.Status == RippleToFITransactionStatus.LockeByProcessor)
            {
                if (this.State.Locked && this.State.OperatorId == managerId)
                {
                    await this.ApplyEvent(new RippleToFIFailedEvent(reason, managerId));
                    return ErrorCode.None;
                }
                else if (this.State.Locked && this.State.OperatorId != managerId)
                {
                    return ErrorCode.RippleToFiIsLockedByOther;
                }
            }
            return ErrorCode.None;
        }

        #endregion

        #region Event Handlers
        private void Handle(RippleToFIInitializedEvent @event)
        {
            this.State.Id = @event.TransactionId;
            this.State.Status = RippleToFITransactionStatus.Initialized;
            this.State.RippleTxId = @event.RippleTxId;
            this.State.InvoiceId = @event.InvoiceId;
            this.State.TransferTargetInfo = @event.TransferTargetInfo;
            this.State.Amount = @event.Amount;
            this.State.SendAmount = @event.SendAmount;
            this.State.Memo = @event.Memo;
            this.State.CreateAt = @event.UTCTimestamp;
            this.State.WriteStateAsync();
        }
        private void Handle(RippleToFILockedEvent @event)
        {
            this.State.Locked = true;
            this.State.Status = RippleToFITransactionStatus.LockeByProcessor;
            this.State.OperatorId = @event.ManagerId;
            this.State.WriteStateAsync();
        }
        private void Handle(RippleToFICompletedEvent @event)
        {
            this.State.Status = RippleToFITransactionStatus.Completed;
            this.State.CompleteAt = @event.UTCTimestamp;
            this.State.WriteStateAsync();
        }
        private void Handle(RippleToFIFailedEvent @event)
        {
            this.State.Status = RippleToFITransactionStatus.Failed;
            this.State.FailAt = @event.UTCTimestamp;
            this.State.WriteStateAsync();
        }
        #endregion
    }


    public interface IRippleToFITransactionState : IEventSourcingState
    {
        long Id { get; set; }
        RippleToFITransactionStatus Status { get; set; }
        string RippleTxId { get; set; }
        string InvoiceId { get; set; }
        TransferTargetInfo TransferTargetInfo { get; set; }
        decimal Amount { get; set; }
        decimal SendAmount { get; set; }
        string Memo { get; set; }
        string ManagerMemo { get; set; }
        bool Locked { get; set; }
        Guid? OperatorId { get; set; }
        DateTime CreateAt { get; set; }
        DateTime? CompleteAt { get; set; }
        DateTime? FailAt { get; set; }
    }
}
