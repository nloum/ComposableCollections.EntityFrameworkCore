using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive.Subjects;
using AutoMapper;
using FluentAssertions;
using LiveLinq;
using LiveLinq.Dictionary;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UtilityDisposables;

namespace ComposableCollections.EntityFrameworkCore.Tests
{
    public class WorkItemDto
    {
        public Guid Id { get; set; }
        public string Description { get; set; }
        public PersonDto AssignedTo { get; set; }
        public Guid? AssignedToForeignKey { get; set; }
    }

    public class PersonDto
    {
        public PersonDto()
        {
        }

        public Guid Id { get; set; }
        public string Name { get; set; }
        public ICollection<WorkItemDto> AssignedWorkItems { get; set; }
    }
    
    public class WorkItem {
        // public WorkItem(Guid id)
        // {
        //     Id = id;
        // }

        public Guid Id { get; set; }
        public string Description { get; set; }
        public Person AssignedTo { get; set; }
    }

    public class Person
    {
        // public Person(Guid id)
        // {
        //     Id = id;
        // }

        public Guid Id { get; set; }
        public string Name { get; set; }
        public ICollection<WorkItem> AssignedWorkItems { get; set; }
    }

    public class MyDbContext : DbContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PersonDto>()
                .HasMany(x => x.AssignedWorkItems)
                .WithOne(x => x.AssignedTo)
                .HasForeignKey(x => x.AssignedToForeignKey);

            base.OnModelCreating(modelBuilder);
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Data Source=tasks.db");
        }
        
        public DbSet<WorkItemDto> WorkItem { get; set; }
        public DbSet<PersonDto> Person { get; set; }
    }

    public static class Transaction
    {
        public static Transaction<TPeople, TTasks> Create<TPeople, TTasks>(TPeople people, TTasks tasks, IDisposable disposable)
        {
            return new Transaction<TPeople, TTasks>(people, tasks, disposable);
        }
    }
    
    public class Transaction<TPeople, TTasks> : IDisposable
    {
        private readonly IDisposable _disposable;

        public Transaction(TPeople people, TTasks tasks, IDisposable disposable)
        {
            _disposable = disposable;
            People = people;
            Tasks = tasks;
        }

        public TPeople People { get; }
        public TTasks Tasks { get; }

        public void Dispose()
        {
            _disposable.Dispose();
        }
    }

    [TestClass]
    public class DatabaseLayerTests
    {
        [TestMethod]
        public void ShouldHandleManyToOneRelationshipsWithMultipleTypesOfEntitiesInOneTransaction()
        {
            if (File.Exists("tasks.db"))
            {
                File.Delete("tasks.db");
            }
            
            var preserveReferencesState = new PreserveReferencesState();
            
            var mapperConfig = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<WorkItem, WorkItemDto>()
                    .ConstructUsing(preserveReferencesState)
                    .ReverseMap();
                    //.ConstructUsing(preserveReferencesState, dto => new WorkItem(dto.Id));

                    cfg.CreateMap<Person, PersonDto>()
                        .ConstructUsing(preserveReferencesState)
                        .ReverseMap();
                    //.ConstructUsing(preserveReferencesState, dto => new Person(dto.Id));
            });

            var mapper = mapperConfig.CreateMapper();
            
            var peopleChanges = new Subject<IDictionaryChangeStrict<Guid, Person>>();
            var taskChanges = new Subject<IDictionaryChangeStrict<Guid, WorkItem>>();

            var transactionalDatabase = new DbContextFactory<MyDbContext>(() => new MyDbContext(), x => x.Database.Migrate());
            var infrastructure = transactionalDatabase.Select(
                dbContext =>
                {
                    var tasks = dbContext.AsQueryableReadOnlyDictionary(x => x.WorkItem, x => x.Id)
                        .WithMapping<Guid, WorkItemDto, WorkItem>(x => x.Id, mapper)
                        .WithChangeNotifications(taskChanges)
                        .WithBuiltInKey(t => t.Id);
                    var people = dbContext.AsQueryableReadOnlyDictionary(x => x.Person, x => x.Id)
                        .WithMapping<Guid, PersonDto, Person>(x => x.Id, mapper)
                        .WithChangeNotifications(peopleChanges)
                        .WithBuiltInKey(p => p.Id);
                    return Transaction.Create(people, tasks, dbContext);
                },
                dbContext =>
                {
                    var tasks = dbContext.AsQueryableDictionary(x => x.WorkItem, x => x.Id)
                        .WithMapping<Guid, WorkItemDto, WorkItem>(x => x.Id, mapper)
                        .WithChangeNotifications(taskChanges, taskChanges.OnNext)
                        .WithBuiltInKey(t => t.Id);
                    var people = dbContext.AsQueryableDictionary(x => x.Person, x => x.Id)
                        .WithMapping<Guid, PersonDto, Person>(x => x.Id, mapper)
                        .WithChangeNotifications(peopleChanges, peopleChanges.OnNext)
                        .WithBuiltInKey(p => p.Id);
                    return Transaction.Create(people, tasks, new AnonymousDisposable(() =>
                    {
                        dbContext.SaveChanges();
                        dbContext.Dispose();
                    }));
                });

            var joeId = Guid.NewGuid();
            var taskId = Guid.NewGuid();
            
            using (var transaction = infrastructure.CreateWriter())
            {
                var joe = new Person()
                {
                    Id = joeId,
                    Name = "Joe"
                };

                transaction.People.Add(joe);
            
                var washTheCar = new WorkItem()
                {
                    Id = taskId,
                    Description = "Wash the car",
                    AssignedTo = joe
                };
            
                transaction.Tasks.Add(washTheCar);   
            }

            using (var transaction = infrastructure.CreateWriter())
            {
                // var joe = transaction.People[joeId];
                // var washTheCar = transaction.Tasks[taskId];
                // joe.Name.Should().Be("Joe");
                // joe.Id.Should().Be(joeId);
                // joe.AssignedWorkItems.Count.Should().Be(1);
                // joe.AssignedWorkItems.First().Id.Should().Be(taskId);
                // joe.AssignedWorkItems.First().Description.Should().Be("Wash the car");
                //
                // washTheCar.Id.Should().Be(taskId);
                // washTheCar.Description.Should().Be("Wash the car");
                // washTheCar.AssignedTo.Id.Should().Be(joeId);
                // washTheCar.AssignedTo.Name.Should().Be("Joe");
            }
        }
        
        [TestMethod]
        public void ShouldHandleManyToOneRelationshipsWithOneTypeOfEntitiesInOneTransaction()
        {
            if (File.Exists("tasks.db"))
            {
                File.Delete("tasks.db");
            }
            
            var preserveReferencesState = new PreserveReferencesState();
            
            var mapperConfig = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<WorkItem, WorkItemDto>()
                    .ConstructUsing(preserveReferencesState)
                    .ReverseMap();
                    //.ConstructUsing(preserveReferencesState, dto => new WorkItem(dto.Id));

                    cfg.CreateMap<Person, PersonDto>()
                        .ConstructUsing(preserveReferencesState)
                        .ReverseMap();
                    //.ConstructUsing(preserveReferencesState, dto => new Person(dto.Id));
            });

            var mapper = mapperConfig.CreateMapper();
            
            var peopleChanges = new Subject<IDictionaryChangeStrict<Guid, Person>>();
            var taskChanges = new Subject<IDictionaryChangeStrict<Guid, WorkItem>>();

            var dbContextFactory = new DbContextFactory<MyDbContext>(() => new MyDbContext(), x => x.Database.Migrate());
            var people = dbContextFactory
                .WithDatabaseTable(x => x.Person, x => x.Id)
                .WithMapping<Guid, PersonDto, Person>(x => x.Id, mapper)
                .WithChangeNotifications(peopleChanges, peopleChanges.OnNext)
                .WithBuiltInKey(x => x.Id);
            var workItems = dbContextFactory
                .WithDatabaseTable(x => x.WorkItem, x => x.Id)
                .WithMapping<Guid, WorkItemDto, WorkItem>(x => x.Id, mapper)
                .WithChangeNotifications(taskChanges, taskChanges.OnNext)
                .WithBuiltInKey(x => x.Id);

            var joeId = Guid.NewGuid();
            var taskId = Guid.NewGuid();
            
            using (var peopleRepo = people.CreateWriter())
            using (var workItemRepo = workItems.CreateWriter())
            {
                var joe = new Person()
                {
                    Id = joeId,
                    Name = "Joe"
                };

                peopleRepo.Add(joe);
            
                var washTheCar = new WorkItem()
                {
                    Id = taskId,
                    Description = "Wash the car",
                    AssignedTo = joe
                };
            
                workItemRepo.Add(washTheCar);   
            }

            using (var peopleRepo = people.CreateReader())
            using (var workItemRepo = workItems.CreateReader())
            {
                // var joe = peopleRepo[joeId];
                // var washTheCar = workItemRepo[taskId];
                // joe.Name.Should().Be("Joe");
                // joe.Id.Should().Be(joeId);
                // joe.AssignedWorkItems.Count.Should().Be(1);
                // joe.AssignedWorkItems.First().Id.Should().Be(taskId);
                // joe.AssignedWorkItems.First().Description.Should().Be("Wash the car");
                //
                // washTheCar.Id.Should().Be(taskId);
                // washTheCar.Description.Should().Be("Wash the car");
                // washTheCar.AssignedTo.Id.Should().Be(joeId);
                // washTheCar.AssignedTo.Name.Should().Be("Joe");
            }
        }
    }
}
