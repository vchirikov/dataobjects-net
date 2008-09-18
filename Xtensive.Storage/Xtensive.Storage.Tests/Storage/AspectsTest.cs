// Copyright (C) 2008 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: 
// Created:    2008.08.21

using System;
using System.Reflection;
using NUnit.Framework;
using Xtensive.Core.Aspects;
using Xtensive.Storage.Attributes;
using Xtensive.Storage.Configuration;
using Xtensive.Storage.Aspects;

namespace Xtensive.Storage.Tests.Storage.AspectsTest
{
  public class AspectsTest : AutoBuildTest
  {
    [Persistent]
    [HierarchyRoot(typeof (Generator), "ID")]
    public class BusinessObject : Entity
    {
      [Field]
      public int ID { get; private set; }

      public void PublicMethod(Action<BusinessObject> callback)
      {        
        callback.Invoke(this);
      }

      [Infrastructure]
      public void InfrastructureMethod(Action<BusinessObject> callback)
      {
        callback.Invoke(this);
      }      
      
      [Infrastructure]
      public void CallPrivateMethod(Action<BusinessObject> callback)
      {
        PrivateMethod(callback);
      }

      private void PrivateMethod(Action<BusinessObject> callback)
      {
        callback.Invoke(this);
      }

      [Infrastructure]
      public void CallProtectedMethod(Action<BusinessObject> callback)
      {
        ProtectedMethod(callback);
      }

      protected void ProtectedMethod(Action<BusinessObject> callback)
      {
        callback.Invoke(this);
      }

      [Infrastructure]
      public void CallInternalMethod(Action<BusinessObject> callback)
      {
        InternalMethod(callback);
      }

      internal void InternalMethod(Action<BusinessObject> callback)
      {
        callback.Invoke(this);
      }

    }

    protected override DomainConfiguration BuildConfiguration()
    {
      DomainConfiguration config = base.BuildConfiguration();
      config.Types.Register(Assembly.GetExecutingAssembly(), "Xtensive.Storage.Tests.Storage.AspectsTest");
      return config;
    }
    
    [Test]
    public void TransactionalAspectTest()
    {
      using (Domain.OpenSession()) {
        BusinessObject obj;
        using (Transaction.Open()) {
          obj = new BusinessObject();
        }
        obj.PublicMethod(
          o => Assert.IsNotNull(o.Session.Transaction));

        obj.InfrastructureMethod(
          o => Assert.IsNull(o.Session.Transaction));

        obj.CallInternalMethod(
          o => Assert.IsNull(o.Session.Transaction));

        obj.CallProtectedMethod(
          o => Assert.IsNull(o.Session.Transaction));

        obj.CallPrivateMethod(
          o => Assert.IsNull(o.Session.Transaction));
      }
    }

    [Test]
    public void AspectsPriorityTest()
    {            
      using (Domain.OpenSession()) {
        using (Transaction.Open()) {
          BusinessObject obj = new BusinessObject();

          using (Domain.OpenSession()) {
            Session session2 = Session.Current;
            Assert.AreNotEqual(obj.Session, session2);

            // Check that transaction will be started in obj.Session, but not in second session.

            obj.PublicMethod(
              o => Assert.IsNotNull(o.Session.Transaction));

            obj.PublicMethod(
              o => Assert.IsNull(session2.Transaction));
          }
        }
      }      
    }
  }
}