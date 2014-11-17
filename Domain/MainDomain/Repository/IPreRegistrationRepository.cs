﻿using FC.Framework.Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DotPay.MainDomain.Repository
{
    public interface IPreRegistrationRepository : IRepository
    {
        PreRegistration FindByEmail(string email);
    }
}
