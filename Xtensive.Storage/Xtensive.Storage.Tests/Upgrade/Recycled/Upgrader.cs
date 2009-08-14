// Copyright (C) 2009 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Ivan Galkin
// Created:    2009.05.20

using System;
using System.Collections.Generic;
using System.Linq;
using Xtensive.Core.Collections;
using Xtensive.Core.Disposing;
using Xtensive.Core.Helpers;
using Xtensive.Storage.Upgrade;
using Xtensive.Storage.Tests.Upgrade.Recycled.Model.Version2;

namespace Xtensive.Storage.Tests.Upgrade.Recycled
{
  [Serializable]
  public class Upgrader : UpgradeHandler
  {
    private static bool isEnabled = false;
    private static string runningVersion;

    /// <exception cref="InvalidOperationException">Handler is already enabled.</exception>
    public static IDisposable Enable(string version)
    {
      if (isEnabled)
        throw new InvalidOperationException();
      isEnabled = true;
      runningVersion = version;
      return new Disposable(_ => {
        isEnabled = false;
        runningVersion = null;
      });
    }

    public override bool IsEnabled {
      get {
        return isEnabled;
      }
    }
    
    protected override string DetectAssemblyVersion()
    {
      return runningVersion;
    }

    public override bool CanUpgradeFrom(string oldVersion)
    {
      return true;
    }

    protected override void AddUpgradeHints()
    {
      var context = UpgradeContext.Current;

      if (runningVersion=="2")
        Version1To2Hints.Apply(hint=>context.Hints.Add(hint));
    }

    public override void OnUpgrade()
    {
      var rcCustomers = Query<RcCustomer>.All;
      var rcEmployees = Query<RcEmployee>.All;
      var orders = Query<Order>.All;
      var customers = Query<Customer>.All;
      var employees = Query<Employee>.All;
      
      rcCustomers.Apply(rcCustomer =>
        new Customer(rcCustomer.Id) {
          Address = rcCustomer.Address,
          Phone = rcCustomer.Phone,
          Name = rcCustomer.Name
        });
      rcEmployees.Apply(rcEmployee =>
        new Employee(rcEmployee.Id) {
          CompanyName = rcEmployee.CompanyName,
          Name = rcEmployee.Name
        });
      var log = new List<string>();
      foreach (var order in orders) {
        var newCustomer = customers.First(customer => customer.Id==order.RcCustomer.Id);
        order.Customer = newCustomer;
        var newEmployee = employees.First(employee => employee.Id==order.RcEmployee.Id);
        order.Employee = newEmployee;
        log.Add(order.ToString());
      }
      foreach (var order in orders)
        log.Add(order.ToString());
      Log.Info("Orders: {0}", Environment.NewLine + 
        string.Join(Environment.NewLine, log.ToArray()));

      // orders.Apply(order => {
      //   order.Customer = customers.First(customer => customer.Id==order.RcCustomer.Id);
      //   order.Employee = employees.First(employee => employee.Id==order.RcEmployee.Id);
      // });
    }

    public override bool IsTypeAvailable(Type type, UpgradeStage upgradeStage)
    {
      string suffix = ".Version" + runningVersion;
      var originalNamespace = type.Namespace;
      var nameSpace = originalNamespace.TryCutSuffix(suffix);
      return nameSpace!=originalNamespace 
        && base.IsTypeAvailable(type, upgradeStage);
    }

    private static IEnumerable<UpgradeHint> Version1To2Hints
    {
      get {
        // renaming types
        yield return new RenameTypeHint(
          "Xtensive.Storage.Tests.Upgrade.Recycled.Model.Version1.Order", typeof (Order));
      }
    }
  }
}