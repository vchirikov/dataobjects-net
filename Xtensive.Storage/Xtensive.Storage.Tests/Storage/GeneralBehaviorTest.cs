// Copyright (C) 2010 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Alexander Nikolaev
// Created:    2010.02.17

using System;
using System.Diagnostics;
using NUnit.Framework;
using Xtensive.Storage.Resources;
using Xtensive.Storage.Tests.Storage.GeneralBehaviorTestModel;

namespace Xtensive.Storage.Tests.Storage.GeneralBehaviorTestModel
{
  [Serializable]
  [HierarchyRoot]
  public abstract class Person : Entity
  {
    [Key, Field]
    public int Id { get; private set; }

    [Field(Length = 50)]
    public string Name { get; set; }
  }

  [Serializable]
  public class Customer : Person
  {
    [Field, Association(PairTo = "Customer")]
    public EntitySet<Order> Orders { get; private set; }
  }

  [Serializable]
  [HierarchyRoot]
  public class Order : Entity
  {
    [Key, Field]
    public int Id { get; private set; }

    [Field]
    public int Number { get; set; }

    [Field]
    public Customer Customer { get; set; }
  }

  [Serializable]
  [HierarchyRoot]
  [KeyGenerator(KeyGeneratorKind.None)]
  public class CustomerOrder : Entity
  {
    [Key(0), Field]
    public int Id { get; private set; }

    [Key(1), Field]
    public Customer Customer { get; private set; }

    [Field]
    public int Number { get; set; }


    // Constructors

    public CustomerOrder(int id, Customer customer)
      : base(id, customer)
    {}
  }
}

namespace Xtensive.Storage.Tests.Storage
{
  [TestFixture]
  public sealed class GeneralBehaviorTest : AutoBuildTest
  {
    protected override Xtensive.Storage.Configuration.DomainConfiguration BuildConfiguration()
    {
      var configuration = base.BuildConfiguration();
      configuration.UpgradeMode = DomainUpgradeMode.Recreate;
      configuration.Types.Register(typeof (Customer).Assembly, typeof (Customer).Namespace);
      return configuration;
    }

    [Test]
    public void AssignReferenceToRemovedEntityTest()
    {
      using (var session = Session.Open(Domain))
      using (var tx = Transaction.Open()) {
        var customer = new Customer();
        customer.Remove();
        var order = new Order();
        AssertEntityRemovalHasBeenDetected(() => order.Customer = customer);
        tx.Complete();
      }
    }

    [Test]
    public void UseRemovedEntityAsKeyForOtherEntityTest()
    {
      using (var session = Session.Open(Domain))
      using (var tx = Transaction.Open()) {
        var customer = new Customer();
        customer.Remove();
        AssertEntityRemovalHasBeenDetected(() => new CustomerOrder(1, customer));
        tx.Complete();
      }
    }

    [Test]
    public void AddRemovedEntityToEntitySetTest()
    {
      using (var session = Session.Open(Domain))
      using (var tx = Transaction.Open()) {
        var customer = new Customer();
        var order = new Order();
        order.Remove();
        AssertEntityRemovalHasBeenDetected(() => customer.Orders.Add(order));
        tx.Complete();
      }
    }

    [Test]
    public void RemoveRemovedEntityFromEntitySetTest()
    {
      using (var session = Session.Open(Domain))
      using (var tx = Transaction.Open()) {
        var customer = new Customer();
        var order = new Order();
        order.Remove();
        AssertEntityRemovalHasBeenDetected(() => customer.Orders.Remove(order));
        tx.Complete();
      }
    }

    [Test]
    public void CallContainsOnEntitySetForRemovedEntityTest()
    {
      using (var session = Session.Open(Domain))
      using (var tx = Transaction.Open()) {
        var customer = new Customer();
        var order = new Order();
        order.Remove();
        AssertEntityRemovalHasBeenDetected(() => customer.Orders.Contains(order));
        tx.Complete();
      }
    }

    private static void AssertEntityRemovalHasBeenDetected(Action action)
    {
      try {
        action.Invoke();
      }
      catch (InvalidOperationException e) {
        Assert.AreEqual(Strings.ExEntityIsRemoved, e.Message);
      }
    }
  }
}