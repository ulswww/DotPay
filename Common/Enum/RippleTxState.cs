﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FC.Framework;

namespace DotPay.Common
{ 
    public enum TransactionState
    {
        Init =0,
        Pending = 1,
        Fail = 2,
        Success = 3
    }
}
