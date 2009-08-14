// Copyright (C) 2009 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Ivan Galkin
// Created:    2009.05.20

using System.Linq;
using NUnit.Framework;
using System.Reflection;
using Xtensive.Core.Disposing;
using M1 = Xtensive.Storage.Tests.Upgrade.Sample3.Model.Version1;
using M2 = Xtensive.Storage.Tests.Upgrade.Sample3.Model.Version2;

namespace Xtensive.Storage.Tests.Upgrade.Sample3
{
  [TestFixture, Category("Upgrade")]
  public class Sample3UpgradeTest
  {
    private Domain domain;

    [SetUp]
    public void SetUp()
    {
      BuildDomain("1", DomainUpgradeMode.Recreate);
      FillData();
    }
    
    [Test]
    public void UpgradeToVersion2Test()
    {
      BuildDomain("2", DomainUpgradeMode.Perform);
      using (Session.Open(domain)) {
        using (Transaction.Open()) {
          Assert.AreEqual(1, Query<M2.Employee>.All.Count());
          var emp = Query<M2.Employee>.All.FirstOrDefault();
          Assert.AreEqual("Sales", emp.DepartmentName);
        }
      }
    }

    private void BuildDomain(string version, DomainUpgradeMode upgradeMode)
    {
      if (domain != null)
        domain.DisposeSafely();

      var configuration = DomainConfigurationFactory.Create();
      configuration.UpgradeMode = upgradeMode;
      configuration.Types.Register(Assembly.GetExecutingAssembly(),
        "Xtensive.Storage.Tests.Upgrade.Sample3.Model.Version" + version);
      using (Upgrader.Enable(version)) {
        domain = Domain.Build(configuration);
      }
    }

    #region Data filler

    private void FillData()
    {
      using (Session.Open(domain)) {
        using (var transactionScope = Transaction.Open()) {
          new M1.Employee {
            FirstName = "Andrew",
            LastName = "Fuller",
            Department = "Sales",
            IsHead = false
          };
          transactionScope.Complete();
        }
      }
    }

    #endregion
  }
}