using NBitcoin;
using System.Linq;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Tests.Helpers;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.BlockchainAnalysis;

public class CoinJoinAnonScoreTests
{
	[Fact]
	public void BasicCalculation()
	{
		var analyser = ServiceFactory.CreateBlockchainAnalyzer();
		var tx = BitcoinFactory.CreateSmartTransaction(9, Enumerable.Repeat(Money.Coins(1m), 9), new[] { (Money.Coins(1.1m), 1) }, new[] { (Money.Coins(1m), HdPubKey.DefaultHighAnonymitySet) });

		analyser.Analyze(tx);

		Assert.Equal(1, tx.WalletInputs.First().HdPubKey.AnonymitySet);

		// 10 participants, 1 is you, your anonset is 10.
		Assert.Equal(10, tx.WalletOutputs.First().HdPubKey.AnonymitySet);
	}

	[Fact]
	public void DoubleProcessing()
	{
		var analyser = ServiceFactory.CreateBlockchainAnalyzer();
		var tx = BitcoinFactory.CreateSmartTransaction(9, Enumerable.Repeat(Money.Coins(1m), 9), new[] { (Money.Coins(1.1m), 1) }, new[] { (Money.Coins(1m), HdPubKey.DefaultHighAnonymitySet) });
		analyser.Analyze(tx);
		analyser.Analyze(tx);
		Assert.Equal(1, tx.WalletInputs.First().HdPubKey.AnonymitySet);

		// 10 participants, 1 is you, your anonset is 10.
		Assert.Equal(10, tx.WalletOutputs.First().HdPubKey.AnonymitySet);
	}

	[Fact]
	public void OtherWalletChangesThings()
	{
		var analyser = ServiceFactory.CreateBlockchainAnalyzer();
		var tx = BitcoinFactory.CreateSmartTransaction(9, Enumerable.Repeat(Money.Coins(1m), 8), new[] { (Money.Coins(1.1m), 1) }, new[] { (Money.Coins(1m), HdPubKey.DefaultHighAnonymitySet), (Money.Coins(1m), HdPubKey.DefaultHighAnonymitySet) });
		var sc = tx.WalletOutputs.First();
		tx.WalletOutputs.Remove(sc);
		analyser.Analyze(tx);
		tx.WalletOutputs.Add(sc);
		analyser.Analyze(tx);
		Assert.Equal(1, tx.WalletInputs.First().HdPubKey.AnonymitySet);

		// 10 participants, 2 is you, your anonset is 10/2 = 5.
		Assert.Equal(5, tx.WalletOutputs.First().HdPubKey.AnonymitySet);
		Assert.Equal(5, tx.WalletOutputs.Skip(1).First().HdPubKey.AnonymitySet);
	}

	[Fact]
	public void Inheritance()
	{
		var analyser = ServiceFactory.CreateBlockchainAnalyzer();
		var tx = BitcoinFactory.CreateSmartTransaction(9, Enumerable.Repeat(Money.Coins(1m), 9), new[] { (Money.Coins(1.1m), 100) }, new[] { (Money.Coins(1m), HdPubKey.DefaultHighAnonymitySet) });

		analyser.Analyze(tx);

		Assert.Equal(100, tx.WalletInputs.First().HdPubKey.AnonymitySet);

		// 10 participants, 1 is you, your anonset is 10 and you inherit 99 anonset,
		// because you don't want to count yourself twice.
		Assert.Equal(109, tx.WalletOutputs.First().HdPubKey.AnonymitySet);
	}

	[Fact]
	public void ChangeOutput()
	{
		var analyser = ServiceFactory.CreateBlockchainAnalyzer();
		var tx = BitcoinFactory.CreateSmartTransaction(9, Enumerable.Repeat(Money.Coins(1m), 9), new[] { (Money.Coins(6.2m), 1) }, new[] { (Money.Coins(1m), HdPubKey.DefaultHighAnonymitySet), (Money.Coins(5m), HdPubKey.DefaultHighAnonymitySet) });

		analyser.Analyze(tx);

		var active = tx.WalletOutputs.First(x => x.Amount == Money.Coins(1m));
		var change = tx.WalletOutputs.First(x => x.Amount == Money.Coins(5m));

		Assert.Equal(1, tx.WalletInputs.First().HdPubKey.AnonymitySet);
		Assert.Equal(10, active.HdPubKey.AnonymitySet);
		Assert.Equal(1, change.HdPubKey.AnonymitySet);
	}

	[Fact]
	public void ChangeOutputInheritance()
	{
		var analyser = ServiceFactory.CreateBlockchainAnalyzer();
		var tx = BitcoinFactory.CreateSmartTransaction(9, Enumerable.Repeat(Money.Coins(1m), 9), new[] { (Money.Coins(6.2m), 100) }, new[] { (Money.Coins(1m), HdPubKey.DefaultHighAnonymitySet), (Money.Coins(5m), HdPubKey.DefaultHighAnonymitySet) });

		analyser.Analyze(tx);

		var active = tx.WalletOutputs.First(x => x.Amount == Money.Coins(1m));
		var change = tx.WalletOutputs.First(x => x.Amount == Money.Coins(5m));

		Assert.Equal(100, tx.WalletInputs.First().HdPubKey.AnonymitySet);
		Assert.Equal(109, active.HdPubKey.AnonymitySet);
		Assert.Equal(100, change.HdPubKey.AnonymitySet);
	}

	[Fact]
	public void MultiDenomination()
	{
		// Multiple standard denomination outputs should be accounted separately.
		var analyser = ServiceFactory.CreateBlockchainAnalyzer();
		var othersOutputs = new[] { 1, 1, 1, 2, 2 };
		var tx = BitcoinFactory.CreateSmartTransaction(
			9,
			othersOutputs.Select(x => Money.Coins(x)),
			new[] { (Money.Coins(3.2m), 1) },
			new[] { (Money.Coins(1m), HdPubKey.DefaultHighAnonymitySet), (Money.Coins(2m), HdPubKey.DefaultHighAnonymitySet) });

		analyser.Analyze(tx);

		var level1 = tx.WalletOutputs.First(x => x.Amount == Money.Coins(1m));
		var level2 = tx.WalletOutputs.First(x => x.Amount == Money.Coins(2m));

		Assert.Equal(1, tx.WalletInputs.First().HdPubKey.AnonymitySet);
		Assert.Equal(4, level1.HdPubKey.AnonymitySet);
		Assert.Equal(3, level2.HdPubKey.AnonymitySet);
	}

	[Fact]
	public void MultiDenominationInheritance()
	{
		// Multiple denominations inherit properly.
		var analyser = ServiceFactory.CreateBlockchainAnalyzer();
		var othersOutputs = new[] { 1, 1, 1, 2, 2 };
		var tx = BitcoinFactory.CreateSmartTransaction(
			9,
			othersOutputs.Select(x => Money.Coins(x)),
			new[] { (Money.Coins(3.2m), 100) },
			new[] { (Money.Coins(1m), HdPubKey.DefaultHighAnonymitySet), (Money.Coins(2m), HdPubKey.DefaultHighAnonymitySet) });

		analyser.Analyze(tx);

		var level1 = tx.WalletOutputs.First(x => x.Amount == Money.Coins(1m));
		var level2 = tx.WalletOutputs.First(x => x.Amount == Money.Coins(2m));

		Assert.Equal(100, tx.WalletInputs.First().HdPubKey.AnonymitySet);
		Assert.Equal(103, level1.HdPubKey.AnonymitySet);
		Assert.Equal(102, level2.HdPubKey.AnonymitySet);
	}

	[Fact]
	public void SelfAnonsetSanityCheck()
	{
		// If we have multiple same denomination in the same coinjoin, then our anonset would be total coins/our coins.
		var analyser = ServiceFactory.CreateBlockchainAnalyzer();
		var othersOutputs = new[] { 1, 1, 1 };
		var ownOutputs = new[] { 1, 1 };
		var tx = BitcoinFactory.CreateSmartTransaction(
			9,
			othersOutputs.Select(x => Money.Coins(x)),
			new[] { (Money.Coins(3.2m), 1) },
			ownOutputs.Select(x => (Money.Coins(x), HdPubKey.DefaultHighAnonymitySet)));

		analyser.Analyze(tx);

		Assert.Equal(1, tx.WalletInputs.First().HdPubKey.AnonymitySet);
		Assert.All(tx.WalletOutputs, x => Assert.Equal(5 / 2, x.HdPubKey.AnonymitySet));
	}

	[Fact]
	public void SelfAnonsetSanityCheck2()
	{
		var analyser = ServiceFactory.CreateBlockchainAnalyzer();
		var othersOutputs = new[] { 1 };
		var ownOutputs = new[] { 1, 1, 1, 1 };
		var tx = BitcoinFactory.CreateSmartTransaction(
				1,
				othersOutputs.Select(x => Money.Coins(x)),
				new[] { (Money.Coins(4.2m), 4) },
				ownOutputs.Select(x => (Money.Coins(x), HdPubKey.DefaultHighAnonymitySet)));

		Assert.Equal(4, tx.WalletInputs.First().HdPubKey.AnonymitySet);
		analyser.Analyze(tx);
		Assert.Equal(4, tx.WalletInputs.First().HdPubKey.AnonymitySet);

		// The increase in the anonymity set would naively be 1 as there is 1 equal non-wallet output.
		// Since 4 outputs are ours, we divide the increase in anonymity between them
		// and add that to the inherited anonymity of 4.
		Assert.All(tx.WalletOutputs, x => Assert.Equal(4 + 1 / 4, x.HdPubKey.AnonymitySet));
	}

	[Fact]
	public void InputSanityCheck()
	{
		// Anonset can never be larger than the number of inputs.
		var analyser = ServiceFactory.CreateBlockchainAnalyzer();
		var tx = BitcoinFactory.CreateSmartTransaction(2, Enumerable.Repeat(Money.Coins(1m), 9), new[] { (Money.Coins(1.1m), 1) }, new[] { (Money.Coins(1m), HdPubKey.DefaultHighAnonymitySet) });

		analyser.Analyze(tx);

		Assert.Equal(1, tx.WalletInputs.First().HdPubKey.AnonymitySet);
		Assert.Equal(3, tx.WalletOutputs.First().HdPubKey.AnonymitySet);
	}

	[Fact]
	public void InputSanityBeforeSelfAnonsetSanityCheck()
	{
		// Input sanity check is executed before self anonset sanity check.
		var analyser = ServiceFactory.CreateBlockchainAnalyzer();
		var othersOutputs = new[] { 1, 1, 1 };
		var ownOutputs = new[] { 1, 1 };
		var tx = BitcoinFactory.CreateSmartTransaction(
			2,
			othersOutputs.Select(x => Money.Coins(x)),
			new[] { (Money.Coins(3.2m), 1) },
			ownOutputs.Select(x => (Money.Coins(x), HdPubKey.DefaultHighAnonymitySet)));

		analyser.Analyze(tx);

		Assert.Equal(1, tx.WalletInputs.First().HdPubKey.AnonymitySet);

		// The increase in the anonymity set would naively be 3 as there are 3 equal non-wallet outputs.
		// But there are only 2 non-wallet inputs, so that limits the increase to 2.
		// Since 2 outputs are ours, we divide the increase in anonymity between them and add that
		// to the inherited anonymity, getting an anonymity set of 1 + 2/2 = 2.
		Assert.All(tx.WalletOutputs, x => Assert.Equal(2, x.HdPubKey.AnonymitySet));
	}

	[Fact]
	public void InputMergePunishment()
	{
		// Input merging results in worse inherited anonset.
		var analyser = ServiceFactory.CreateBlockchainAnalyzer();
		var tx = BitcoinFactory.CreateSmartTransaction(
			9,
			Enumerable.Repeat(Money.Coins(1m), 9),
			new[] { (Money.Coins(1.1m), 100), (Money.Coins(1.2m), 200), (Money.Coins(1.3m), 300), (Money.Coins(1.4m), 400) },
			new[] { (Money.Coins(1m), HdPubKey.DefaultHighAnonymitySet) });

		analyser.Analyze(tx);

		Assert.All(tx.WalletInputs, x => Assert.True(x.HdPubKey.AnonymitySet < 100));

		// 10 participants, 1 is you, your added anonset would be 10.
		// Smallest input anonset is 100 so your anonset would be 109 normally, but 4 inputs are merged so it should be worse.
		Assert.True(tx.WalletOutputs.First().HdPubKey.AnonymitySet < 109);
	}

	[Fact]
	public void InputMergePunishmentNoInheritance()
	{
		// Input merging results in worse inherited anonset, but does not punish gains from output indistinguishability.
		var analyser = ServiceFactory.CreateBlockchainAnalyzer();
		var tx = BitcoinFactory.CreateSmartTransaction(
			9,
			Enumerable.Repeat(Money.Coins(1m), 9),
			new[] { (Money.Coins(1.1m), 1), (Money.Coins(1.2m), 1), (Money.Coins(1.3m), 1), (Money.Coins(1.4m), 1) },
			new[] { (Money.Coins(1m), HdPubKey.DefaultHighAnonymitySet) });

		analyser.Analyze(tx);

		Assert.All(tx.WalletInputs, x => Assert.Equal(1, x.HdPubKey.AnonymitySet));

		// 10 participants, 1 is you, your anonset would be 10 normally and now too:
		Assert.Equal(10, tx.WalletOutputs.First().HdPubKey.AnonymitySet);
	}

	[Fact]
	public void InputMergeProportionalPunishment()
	{
		// Input merging more coins results in worse anonset.
		// In this test tx1 consolidates less inputs than tx2.
		var analyser = ServiceFactory.CreateBlockchainAnalyzer();

		var othersInputCount = 9;
		var othersOutputs = Enumerable.Repeat(Money.Coins(1m), 9);
		var ownInputs1 = new[] { (Money.Coins(1.1m), 100), (Money.Coins(1.2m), 200), (Money.Coins(1.3m), 300), (Money.Coins(1.4m), 400) };
		var ownInputs2 = ownInputs1.Concat(new[] { (Money.Coins(1.5m), 100) });
		var ownOutputs = new[] { (Money.Coins(1m), HdPubKey.DefaultHighAnonymitySet) };

		var tx1 = BitcoinFactory.CreateSmartTransaction(
			othersInputCount,
			othersOutputs,
			ownInputs1,
			ownOutputs);

		var tx2 = BitcoinFactory.CreateSmartTransaction(
			othersInputCount,
			othersOutputs,
			ownInputs2,
			ownOutputs);

		analyser.Analyze(tx1);
		analyser.Analyze(tx2);

		Assert.All(tx1.WalletInputs, x => Assert.All(tx2.WalletInputs, y => Assert.True(x.HdPubKey.AnonymitySet > y.HdPubKey.AnonymitySet)));
		Assert.True(tx1.WalletOutputs.First().HdPubKey.AnonymitySet > tx2.WalletOutputs.First().HdPubKey.AnonymitySet);
	}

	[Fact]
	public void InputMergePunishmentDependsOnCjSize()
	{
		// Input merging in larger coinjoin results in less punishmnent.
		var analyser = ServiceFactory.CreateBlockchainAnalyzer();

		var othersInputCount1 = 10;
		var othersInputCount2 = 9;
		var othersOutputs = Enumerable.Repeat(Money.Coins(1m), 9);
		var ownInputs = new[] { (Money.Coins(1.1m), 100), (Money.Coins(1.2m), 200), (Money.Coins(1.3m), 300), (Money.Coins(1.4m), 400) };
		var ownOutputs = new[] { (Money.Coins(1m), HdPubKey.DefaultHighAnonymitySet) };

		var tx1 = BitcoinFactory.CreateSmartTransaction(
			othersInputCount1,
			othersOutputs,
			ownInputs,
			ownOutputs);

		var tx2 = BitcoinFactory.CreateSmartTransaction(
			othersInputCount2,
			othersOutputs,
			ownInputs,
			ownOutputs);

		analyser.Analyze(tx1);
		analyser.Analyze(tx2);

		Assert.All(tx1.WalletInputs, x => Assert.All(tx2.WalletInputs, y => Assert.True(x.HdPubKey.AnonymitySet > y.HdPubKey.AnonymitySet)));
		Assert.True(tx1.WalletOutputs.First().HdPubKey.AnonymitySet > tx2.WalletOutputs.First().HdPubKey.AnonymitySet);
	}

	[Fact]
	public void SiblingCoinjoinDoesntContributeToAnonScore()
	{
		// See full explanation of this example here: https://github.com/zkSNACKs/WalletWasabi/issues/7635#issuecomment-1085978009
		// Create TX1 as a 2 in 2 out coinjoin. Outputs gain 1 anonset.
		var analyser = ServiceFactory.CreateBlockchainAnalyzer();
		var othersOutputs = new[] { 1 };
		var ownOutputs = new[] { 1 };
		var tx1 = BitcoinFactory.CreateSmartTransaction(
				1,
				othersOutputs.Select(x => Money.Coins(x)),
				new[] { (Money.Coins(1m), 1) },
				ownOutputs.Select(x => (Money.Coins(x), HdPubKey.DefaultHighAnonymitySet)));

		Assert.Equal(1, tx1.WalletInputs.First().HdPubKey.AnonymitySet);
		analyser.Analyze(tx1);
		Assert.Equal(1, tx1.WalletInputs.First().HdPubKey.AnonymitySet);
		Assert.All(tx1.WalletOutputs, x => Assert.Equal(2, x.HdPubKey.AnonymitySet));

		// Let's create TX2 that is the same as TX1 except its inputs are TX1: a sybil coinjoin.
		ownOutputs = new[] { 1, 1 };
		var tx2 = BitcoinFactory.CreateSmartTransaction(
				1,
				othersOutputs.Select(x => Money.Coins(x)),
				new[] { (Money.Coins(1m), 1) },
				ownOutputs.Select(x => (Money.Coins(x), HdPubKey.DefaultHighAnonymitySet)));

		// Link up the tx with the previous one.
		tx2.WalletInputs.Clear();
		tx2.WalletInputs.Add(tx1.WalletOutputs.First());
		tx2.Transaction.Inputs.Clear();
		tx2.Transaction.Inputs.Add(tx1.Transaction, 0);
		tx2.Transaction.Inputs.Add(tx1.Transaction, 1);

		Assert.Equal(2, tx2.WalletInputs.First().HdPubKey.AnonymitySet);
		analyser.Analyze(tx2);
		Assert.Equal(2, tx2.WalletInputs.First().HdPubKey.AnonymitySet);
		Assert.All(tx2.WalletOutputs, x => Assert.Equal(2, x.HdPubKey.AnonymitySet));
	}

	[Fact]
	public void EarlierSiblingCoinjoinDoesntContributeToAnonScore()
	{
		// See full explanation of this example here: https://github.com/zkSNACKs/WalletWasabi/issues/7635#issuecomment-1084612032
		// Create TX1 as a 2 in 2 out coinjoin. Outputs gain 1 anonset.
		var analyser = ServiceFactory.CreateBlockchainAnalyzer();
		var othersOutputs = new[] { 2 };
		var ownOutputs = new[] { 2 };
		var tx1 = BitcoinFactory.CreateSmartTransaction(
				1,
				othersOutputs.Select(x => Money.Coins(x)),
				new[] { (Money.Coins(2m), 1) },
				ownOutputs.Select(x => (Money.Coins(x), HdPubKey.DefaultHighAnonymitySet)));

		Assert.Equal(1, tx1.WalletInputs.First().HdPubKey.AnonymitySet);
		analyser.Analyze(tx1);
		Assert.Equal(1, tx1.WalletInputs.First().HdPubKey.AnonymitySet);
		Assert.All(tx1.WalletOutputs, x => Assert.Equal(2, x.HdPubKey.AnonymitySet));

		// Let's create TX2 with 2 input 3 outputs. One of our input is from TX1.
		othersOutputs = new[] { 1 };
		ownOutputs = new[] { 1, 1 };
		var tx2 = BitcoinFactory.CreateSmartTransaction(
				1,
				othersOutputs.Select(x => Money.Coins(x)),
				new[] { (Money.Coins(2m), 1) },
				ownOutputs.Select(x => (Money.Coins(x), HdPubKey.DefaultHighAnonymitySet)));

		// Link up the tx with the previous one.
		tx2.Transaction.Inputs.RemoveAll(x => x.PrevOut == tx2.WalletInputs.First().OutPoint);
		tx2.WalletInputs.Clear();
		tx2.WalletInputs.Add(tx1.WalletOutputs.First());
		tx2.Transaction.Inputs.Add(tx1.Transaction, (int)tx1.WalletOutputs.First().Index);

		Assert.Equal(2, tx2.WalletInputs.First().HdPubKey.AnonymitySet);
		analyser.Analyze(tx2);
		Assert.Equal(2, tx2.WalletInputs.First().HdPubKey.AnonymitySet);
		Assert.All(tx2.WalletOutputs, x => Assert.Equal(2, x.HdPubKey.AnonymitySet));

		// Finally create TX3 that takes 3 inputs and 4 outputs.
		// 1 input comes from TX1, from others.
		// 2 inputs come from TX2. One is ours, one from others.
		// All the amounts are 1 BTC.
		othersOutputs = new[] { 1, 1, 1 };
		ownOutputs = new[] { 1 };
		var tx3 = BitcoinFactory.CreateSmartTransaction(
				2,
				othersOutputs.Select(x => Money.Coins(x)),
				new[] { (Money.Coins(1m), 1) },
				ownOutputs.Select(x => (Money.Coins(x), HdPubKey.DefaultHighAnonymitySet)));

		// Link it up.
		tx3.WalletInputs.Clear();
		tx3.WalletInputs.Add(tx2.WalletOutputs.First());
		tx3.Transaction.Inputs.Clear();
		tx3.Transaction.Inputs.Add(tx1.Transaction, tx1.WalletOutputs.First().Index == 0 ? 1 : 0);
		tx3.Transaction.Inputs.Add(tx2.Transaction, tx2.WalletOutputs.First().Index == 0 ? 1 : 0);
		tx3.Transaction.Inputs.Add(tx2.Transaction, (int)tx2.WalletOutputs.First().Index);

		Assert.Equal(2, tx3.WalletInputs.First().HdPubKey.AnonymitySet);
		analyser.Analyze(tx3);
		Assert.Equal(2, tx3.WalletInputs.First().HdPubKey.AnonymitySet);
		Assert.All(tx3.WalletOutputs, x => Assert.Equal(4, x.HdPubKey.AnonymitySet));
	}
}
