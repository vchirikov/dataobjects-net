// Copyright (C) 2008 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Alexey Kochetov
// Created:    2008.12.17

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Xtensive.Core.Comparison;
using Xtensive.Core.Testing;
using Xtensive.Storage.Linq;
using Xtensive.Storage.Tests.ObjectModel;
using Xtensive.Storage.Tests.ObjectModel.NorthwindDO;
using Region=Xtensive.Storage.Tests.ObjectModel.NorthwindDO.Region;

namespace Xtensive.Storage.Tests.Linq
{
  [Category("Linq")]
  [TestFixture]
  public class JoinTest : NorthwindDOModelTest
  {
    [Test]
    public void GroupJoinAggregateTest()
    {
      var q = Query<Customer>.All
        .GroupJoin(Query<Order>.All,
          customer => customer.Id,
          order => order.Customer.Id,
          (customer, orders) => new {customer, orders})
        .GroupJoin(Query<Employee>.All,
          customerOrders => customerOrders.customer.Address.City,
          employee => employee.Address.City,
          (customerOrders, employees) => new {
            ords = customerOrders.orders.Count(),
            emps = employees.Count()
          });

      var expected = Query<Customer>.All.AsEnumerable()
        .GroupJoin(Query<Order>.All.AsEnumerable(),
          customer => customer.Id,
          order => order.Customer.Id,
          (customer, orders) => new {customer, orders})
        .GroupJoin(Query<Employee>.All.AsEnumerable(),
          customerOrders => customerOrders.customer.Address.City,
          employee => employee.Address.City,
          (customerOrders, employees) => new {
            ords = customerOrders.orders.Count(),
            emps = employees.Count()
          });
      QueryDumper.Dump(expected, true);
      QueryDumper.Dump(q, true);

      Assert.IsTrue(expected.SequenceEqual(q));
    }

    [Test]
    public void GroupJoinAggregate2Test()
    {
      var q = Query<Customer>.All
        .GroupJoin(Query<Order>.All,
          customer => customer.Id,
          order => order.Customer.Id,
          (customer, orders) => new {customer, orders})
        .GroupJoin(Query<Employee>.All,
          customerOrders => customerOrders.customer.Address.City,
          employee => employee.Address.City,
          (customerOrders, employees) => new {
            ords = customerOrders.orders.Count(),
            emps = employees.Count(),
            sum = employees.Count() + customerOrders.orders.Count()
          });

      var expected = Query<Customer>.All.AsEnumerable()
        .GroupJoin(Query<Order>.All.AsEnumerable(),
          customer => customer.Id,
          order => order.Customer.Id,
          (customer, orders) => new {customer, orders})
        .GroupJoin(Query<Employee>.All.AsEnumerable(),
          customerOrders => customerOrders.customer.Address.City,
          employee => employee.Address.City,
          (customerOrders, employees) => new {
            ords = customerOrders.orders.Count(),
            emps = employees.Count(),
            sum = employees.Count() + customerOrders.orders.Count()
          });

      Assert.IsTrue(expected.SequenceEqual(q));

      QueryDumper.Dump(expected, true);
      QueryDumper.Dump(q, true);
    }

    [Test]
    public void SingleTest()
    {
      var products = Query<Product>.All;
      var productsCount = products.Count();
      var suppliers = Query<Supplier>.All;
      var result = from p in products
      join s in suppliers on p.Supplier.Id equals s.Id
      select new {p.ProductName, s.ContactName, s.Phone};
      var list = result.ToList();
      Assert.AreEqual(productsCount, list.Count);
    }

    [Test]
    public void LeftJoin1Test()
    {
      Query<Territory>.All.First().Region = null;
      Session.Current.Persist();
      var territories = Query<Territory>.All;
      var regions = Query<Region>.All;
      var result = territories.JoinLeft(
        regions, 
        territory => territory.Region, 
        region => region, 
        (territory, region) => new {territory.TerritoryDescription, region.RegionDescription});
      foreach (var item in result)
        Console.WriteLine("{0} {1}", item.RegionDescription, item.TerritoryDescription);
      QueryDumper.Dump(result);
    }

    public void LeftJoin2Test()
    {
      Query<Territory>.All.First().Region = null;
      Session.Current.Persist();
      var territories = Query<Territory>.All;
      var regions = Query<Region>.All;
      var result = territories.JoinLeft(
        regions, 
        territory => territory.Region.Id, 
        region => region.Id, 
        (territory, region) => new {territory.TerritoryDescription, region.RegionDescription});
      foreach (var item in result)
        Console.WriteLine("{0} {1}", item.RegionDescription, item.TerritoryDescription);
      QueryDumper.Dump(result);
    }

    [Test]
    public void SeveralTest()
    {
      var products = Query<Product>.All;
      var productsCount = products.Count();
      var suppliers = Query<Supplier>.All;
      var categories = Query<Category>.All;
      var result = from p in products
      join s in suppliers on p.Supplier.Id equals s.Id
      join c in categories on p.Category.Id equals c.Id
      select new {p, s, c.CategoryName};
      var list = result.ToList();
      Assert.AreEqual(productsCount, list.Count);
    }

    [Test]
    public void OneToManyTest()
    {
      var products = Query<Product>.All;
      var productsCount = products.Count();
      var suppliers = Query<Supplier>.All;
      var result = from s in suppliers
      join p in products on s.Id equals p.Supplier.Id
      select new {p.ProductName, s.ContactName};
      var list = result.ToList();
      Assert.AreEqual(productsCount, list.Count);
    }

    [Test]
    public void EntityJoinTest()
    {
      var result =
        from c in Query<Customer>.All
        join o in Query<Order>.All on c equals o.Customer
        select new {c.ContactName, o.OrderDate};
      var list = result.ToList();
    }

    [Test]
    public void AnonymousEntityJoinTest()
    {
      var result =
        from c in Query<Customer>.All
        join o in Query<Order>.All
          on new {Customer = c, Name = c.ContactName} equals new {o.Customer, Name = o.Customer.ContactName}
        select new {c.ContactName, o.OrderDate};
      var list = result.ToList();
    }

    [Test]
    public void JoinByCalculatedColumnTest()
    {
      var customers = Query<Customer>.All;
      var localCustomers = customers.ToList();
      var expected =
        from c1 in localCustomers
        join c2 in localCustomers
          on c1.CompanyName.Substring(0, 1).ToUpper() equals c2.CompanyName.Substring(0, 1).ToUpper()
        select new {l = c1.CompanyName, r = c2.CompanyName};
      var result =
        from c1 in customers
        join c2 in customers
          on c1.CompanyName.Substring(0, 1).ToUpper() equals c2.CompanyName.Substring(0, 1).ToUpper()
        select new {l = c1.CompanyName, r = c2.CompanyName};
      Assert.AreEqual(expected.Count(), result.Count());
    }

    [Test]
    public void GroupJoinTest()
    {
      var categories = Query<Category>.All;
      var products = Query<Product>.All;
      var categoryCount = categories.Count();
      IQueryable<IEnumerable<Product>> result =
        categories.GroupJoin(
          products,
          c => c,
          p => p.Category,
          (c, pGroup) => pGroup);
      var expected =
        categories.AsEnumerable().GroupJoin(
          products.AsEnumerable(),
          c => c,
          p => p.Category,
          (c, pGroup) => pGroup);
      var list = result.ToList();
      Assert.AreEqual(categoryCount, list.Count);
      QueryDumper.Dump(result, true);
    }

    [Test]
    public void GroupJoinWithComparerTest()
    {
      var categories = Query<Category>.All;
      var products = Query<Product>.All;
      var result =
        categories.GroupJoin(
          products,
          c => c.Id,
          p => p.Id,
          (c, pGroup) => pGroup,
          AdvancedComparer<int>.Default.EqualityComparerImplementation);
      AssertEx.Throws<NotSupportedException>(() => result.ToList());
    }

    [Test]
    public void GroupJoinNestedTest()
    {
      var categories = Query<Category>.All;
      var products = Query<Product>.All;
      var categoryCount = categories.Count();
      var result =
        categories.OrderBy(c => c.CategoryName)
          .GroupJoin(products, c => c, p => p.Category, (c, pGroup) => new {
            Category = c.CategoryName,
            Products = pGroup.OrderBy(ip => ip.ProductName)
          });
      var list = result.ToList();
      Assert.AreEqual(categoryCount, list.Count);
      QueryDumper.Dump(result, true);
    }

    [Test]
    public void GroupJoinSelectManyTest()
    {
      using (Domain.OpenSession())
      using (var t = Transaction.Open()) {
        var categories = Query<Category>.All;
        var products = Query<Product>.All;
        var result = categories
          .OrderBy(c => c.CategoryName)
          .GroupJoin(
          products,
          c => c,
          p => p.Category,
          (c, pGroup) => new {c, pGroup})
          .SelectMany(@t1 => @t1.pGroup, (@t1, gp) => new {@t1, gp})
          .OrderBy(@t1 => @t1.gp.ProductName)
          .Select(@t1 => new {Category = @t1.@t1.c.CategoryName, @t1.gp.ProductName})
          ;
        var list = result.ToList();
        QueryDumper.Dump(result, true);
      }
    }

    [Test]
    [ExpectedException(typeof (NotSupportedException))]
    public void DefaultIfEmptyTest()
    {
      var categories = Query<Category>.All;
      var products = Query<Product>.All;
      var categoryCount = categories.Count();
      var result = categories.GroupJoin(
        products,
        c => c,
        p => p.Category,
        (c, pGroup) => pGroup.DefaultIfEmpty());
      var list = result.ToList();
      Assert.AreEqual(categoryCount, list.Count);
      QueryDumper.Dump(result, true);
    }

    [Test]
    public void LeftOuterTest()
    {
      var categories = Query<Category>.All;
      var products = Query<Product>.All;
      var productsCount = products.Count();
      var result = categories.GroupJoin(
        products,
        c => c,
        p => p.Category,
        (c, pGroup) => new {c, pGroup})
        .SelectMany(@t => @t.pGroup.DefaultIfEmpty(), (@t, p) => new {Name = p==null ? "Nothing!" : p.ProductName, @t.c.CategoryName});
      var list = result.ToList();
      Assert.AreEqual(productsCount, list.Count);
      QueryDumper.Dump(result, true);
    }

    [Test]
    public void GroupJoinAnonimousTest()
    {
      var query = Query<Supplier>.All
        .GroupJoin(Query<Product>.All, s => s, p => p.Supplier, (s, products) => new {
          s.CompanyName,
          s.ContactName,
          s.Phone,
          Products = products
        });
      QueryDumper.Dump(query);
    }
  }
}