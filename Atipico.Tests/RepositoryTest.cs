using Moq;
using Moq.EntityFrameworkCore;
using Atipico.Infraestructure.Persistence.Repositories;
using Atipico.Infraestructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Atipico.Tests
{
    public class RepositoryTest
    {
        [Fact]
        public void Add_TestClassObjectPassed_ProperMethodCalled()
        {
            var testObject = new TestClass();

            var context = new Mock<AppDbContext>(new DbContextOptions<AppDbContext>());
            var dbSetMock = new Mock<DbSet<TestClass>>(); 

            context.Setup(x => x.Set<TestClass>()).Returns(dbSetMock.Object);
            dbSetMock.Setup(x => x.Add(It.IsAny<TestClass>()));

            var repository = new Repository<TestClass>(context.Object);

            repository.Add(testObject);

            context.Verify(x => x.Set<TestClass>());
            dbSetMock.Verify(x => x.Add(It.Is<TestClass>(t => t == testObject)));
        }

        [Fact]
        public void Remove_TestClassObjectPassed_ProperMethodCalled()
        {
            var testObject = new TestClass();

            var context = new Mock<AppDbContext>(new DbContextOptions<AppDbContext>());
            var dbSetMock = new Mock<DbSet<TestClass>>();

            context.Setup(x => x.Set<TestClass>()).Returns(dbSetMock.Object);
            dbSetMock.Setup(x => x.Remove(It.IsAny<TestClass>()));

            var repository = new Repository<TestClass>(context.Object);

            repository.Remove(testObject);

            context.Verify(x => x.Set<TestClass>());
            dbSetMock.Verify(x => x.Remove(It.Is<TestClass>(t => t == testObject)));
        }

        [Fact]
        public async Task Get_TestClassObjectPassed_ProperMethodCalled()
        {
            var testObject = new TestClass() { id = 1 };

            var context = new Mock<AppDbContext>(new DbContextOptions<AppDbContext>());
            var dbSetMock = new Mock<DbSet<TestClass>>();

            context.Setup(x => x.Set<TestClass>()).Returns(dbSetMock.Object);
            dbSetMock.Setup(x => x.FindAsync(It.IsAny<object[]>())).ReturnsAsync(testObject);

            var repository = new Repository<TestClass>(context.Object);
            var result = await repository.GetByIdAsync(1);

            context.Verify(x => x.Set<TestClass>());
            dbSetMock.Verify(x => x.FindAsync(It.Is<object[]>(ids => (int)ids[0] == 1)));

            Assert.Equal(testObject, result);
        }

        [Fact]
        public async Task GetAll_TestClassObjectPassed_ProperMethodCalled()
        {
            var testObject = new TestClass() { id = 1 };
            var testList = new List<TestClass> { testObject };

            var dbSetMock = new Mock<DbSet<TestClass>>();
            MoqExtensions.ConfigureMock(dbSetMock, testList);

            var context = new Mock<AppDbContext>(new DbContextOptions<AppDbContext>());
            context.Setup(x => x.Set<TestClass>()).Returns(dbSetMock.Object);

            var repository = new Repository<TestClass>(context.Object);

            var result = await repository.GetAllAsync();

            Assert.Equal(testList, result.ToList());
        }

        [Fact]
        public async Task Find_TestClassObjectPassed_ProperMethodCalled()
        {
            var testObject = new TestClass() { id = 1 };
            var testList = new List<TestClass> { testObject };

            var dbSetMock = new Mock<DbSet<TestClass>>();
            MoqExtensions.ConfigureMock(dbSetMock, testList);

            var context = new Mock<AppDbContext>(new DbContextOptions<AppDbContext>());
            context.Setup(x => x.Set<TestClass>()).Returns(dbSetMock.Object);

            var repository = new Repository<TestClass>(context.Object);

            var result = await repository.FindAsync(t => t.id == 1);

            Assert.Equal(testList, result.ToList());
        }
    }
}