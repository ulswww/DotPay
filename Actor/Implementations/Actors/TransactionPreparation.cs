﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dotpay.Common.Enum;
using Orleans.Concurrency;

namespace Dotpay.Actors.Implementations
{
    [Immutable]
    [Serializable]
    public class TransactionPreparation
    {
        public TransactionPreparation(Guid transactionId, TransactionType transactionType, PreparationType preparationType, CurrencyType currency, decimal amount)
        {
            this.TransactionId = transactionId;
            this.TransactionType = transactionType;
            this.PreparationType = preparationType;
            this.Currency = currency;
            this.Amount = amount;
        }
        public Guid TransactionId { get; set; }
        public TransactionType TransactionType { get; set; }
        public PreparationType PreparationType { get; set; }
        public CurrencyType Currency { get; set; }
        public decimal Amount { get; set; }
    }
}
