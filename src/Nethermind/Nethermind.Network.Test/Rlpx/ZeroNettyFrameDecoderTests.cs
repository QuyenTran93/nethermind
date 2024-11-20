// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using DotNetty.Buffers;
using DotNetty.Common.Utilities;
using Nethermind.Core.Extensions;
using Nethermind.Core.Test.Builders;
using Nethermind.Network.Rlpx;
using Nethermind.Network.Test.Rlpx.TestWrappers;
using NUnit.Framework;

namespace Nethermind.Network.Test.Rlpx;

public class ZeroNettyFrameDecoderTests
{
    private const string ShortNewBlockSingleFrame = "96cd2d950a261eae89f0e0cd0432c7d142d19e629f6b6d653a1424b0b7dcd1a6fc75caed4bbbb6d3c888618db2453158b06548d9bdb1b8208b70e0075d675bbe80268afff07570005454467c0c3e85a3468e3225015521fecb9a0c1a2092fd445f14552e33d4ebb4d7b533606989210b4c702662b4d2417f293b2701ee4336624916cddefb4c3777d1a144e1df66162b60bcaed3319becbbe19062f7e3715505dc84fa23a531d7dfd35b9ad95042a85faa643a40365d52d0b57e67cc428676cc364567a6c8cf7fda48bd4c54fd2c8fad1e504f3c2451f538793c022fcdfaef9d8fdafae2c989a7e61dba88879cc9062b774bc3f999b0cdece9aab3d34fafc1513e2de18e6c297d07a5992cc68adb27ce3de39c290884e2f6f0f8b24645c622b2cd9d396498e689a44dc775c22f4baa6b5c1685c19b020110c5788b0e61ebf1794f05df9237d61ca95f49f00fa0217c6a0935f5674c32f8eb7b9c5fe351d429ba0383761d980b53b1187a9a367f80075c020fcc71a75689a4a9e74b8c6137ab267ce6e697008ff5f8c29af53fd4f97018da197f990181f112be738fd0f57bcfc72a78dda731b64aecdd3f83586f1fcfdbdf2c58eff42b38fed19730a1fd0dd21d248d78a396689a508561590543dbe62b44475bec724f2c13678dc877d6cc15099634c10ee4206b339e45c4829c8d81537c3b57fab792929f79a391c29d4f10f6db8318d9f2bffffdafb3038f005ab5e9ce4bf46960f7cf078cebc4287da0330f6a02555851db05eb28dab481313b43dd64eb9a70e8b4460da9955c39e5c24d2d3432189911b6bbdc05c5c6d50fc8e2422a833c3b9ff407ed69cf8fa55248cddddab62951b60c822588ae6b22c67bd9126a5a84cb5723a3079d52972341bcf216";
    private const string ShortNewBlockSingleFrameDecrypted = "000247c180000000000000000000000007f90243f9023ff901f9a0ff483e972a04a9a62bb4b7d04ae403c615604e4090521ecc5bb7af67f71be09ca01dcc4de8dec75d7aab85b567b6ccd41ad312451b948a7413f0a142fd40d49347940000000000000000000000000000000000000000a056e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421a056e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421a056e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421b9010000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000830f424080833d090080830f424083010203a02ba5557a4c62a513c7e56d1bf13373e0da6bec016755483e91589fe1c6d212e28800000000000003e8f840df80018252089400000000000000000000000000000000000000000180808080df80018252089400000000000000000000000000000000000000000180808080c080000000000000000000";
    private const string BigNewBlockSingleFrame = "96c562950a261eae89f0e0cd0432c7d16fad96fcd58ca3b1c831155745e98a77fc75c2aa4bb389d3c888618db2453158b06548d9bdb1b8208b70e0075d675bbe80268afff07570005454467c0c3e85a3468e3225015521fecb9a0c1a2092fd445f14552e33d4ebb4d7b533606989210b4c702662b4d2417f293b2701ee4336624916cddefb4c3777d1a144e1df66162b60bcaed3319becbbe19062f7e3715505dc84fa23a531d7dfd35b9ad95042a85faa643a40365d52d0b57e67cc428676cc364567a6c8cf7fda48bd4c54fd2c8fad1e504f3c2451f538793c022fcdfaef9d8fdafae2c989a7e61dba88879cc9062b774bc3f999b0cdece9aab3d34fafc1513e2de18e6c297d07a5992cc68adb27ce3de39c290884e2f6f0f8b24645c622b2cd9d396498e689a44dc775c22f4baa6b5c1685c19b020110c5788b0e61ebf1794f05df9237d61ca95f49f00fa0217c6a0935f5674c32f8eb7b9c5fe351d429ba0383761d980b53b1187a9a367f80075c020fcc71a75689a4a9e74b8c6137ab267ce6e697008ff5f8c29af53fd4f97018da197f990181f112be738fd0f57bcfc72a78dda731b64aecdd3f83586f1fcfdbdf2c58eff42b38fed19730a1fd0dd21d248d78a396689a508561590543dbe62b44475bec724f2c13678dc877d6cc15099634c10ee4206b339e45c4829c8d81537c3b57fab792929f79a391c29d4f10f6db8318d9f2bffffdafb3038f005ab5e9ce4bf46960f7cf078cebc4287da0330f6a02555919045a6aab0aee1da53b43dd64eb9a70e8b4460da9955c39e5c24d2d35b3189911e9e45d86159c499bc8e2422a833c3b9ff407ed69cf8fa55248cddddb372951b64cdda5892c392a527bd91230dcb9cf21cbfbc878157866b430e709954d08d53b7e689c01477956bf697eca8dd24a3f51b16c6b15c930500b3c9b9e99620e6cbfbd4c01a35593292b3ab35452c30bd03e44719d96d501b097721f55e5316c0fa3f6eb2cc70965f9798bbd424350a4ce0bb43f9666cb89a710db9ce1f248445d949d8d5b87e4f01ba6f41638dfb200a1788e99f85a896e77fe7a86a17e607d708ca623920705e363e390d0b0025c9b9a5e2ae9e00e34bc1b358e4a787cfa54cfad9384da899bae3887fc745f9e08a21244228c033769c8cadd0215549381282d7f4961c91b2238f8e98310b6953ec92869415c772dfc4581c5bb0aaaf0f65a3e6755d74fdfa49d7eff6154b638e27be6cd9942fefdc615fa93d6b2d66a56aec3c71ba81dd15ec8215048d40b07eeda996a631064b3086520acdaf126b541f4632c63d9822cd6a005d79b1532504211d854e6cad01cf85ff380499909638efc2877b9a2b50f5fc297f620462e314ac10d9bf5b040f27db2df66286318485af830c8af931b300272525aef5c33e7bfaf62622f3575ef9c6e9018209188e1ff3168bf88dd4b1958133eb6a5879fe681868112002df79ee13cdcf5776e04ac6d6b6a461015aee706bc775d335c515703ceda47d59594c883003bb8adcbeed53f188d8e80419bf04dfb13eb23ba7eac216dc71d1af54d6b2c3db2db586b18f544349a00afa8fa64654c8daa5690927c0b0411a06d331023dd2f89670b5779f552c04d333794abf8cac90efc2e0bd8f868a7fc34d8e7ff7cac26e3f01c37f77e8a92383d0a28328767747bada2972f6bcb6eed6e4ec98cb69579a98581c15777408eebffc7ca3167c7b281a8fe4e26d46f337dcbfca91de5785b7556cd2748b4cbc708e90178ca1666a9098fdcd9a10ee65ca37ae9eb5e606e5e2379f116aa345999d82d98f9c0b51a3a132c16d09cb67e4cffb0b36b6e58650062539aa49273e4e64bd05ee720feb0f822707ae29395de5252b7f25e783520273cecf1c1d06a71dfec0abfdec99c34c7ebf708bc800246f17393d218a241191106dfa20891272e7a100e1e8d90d49e18bbbb4f9cea2a0c7de3f6a83f43f76927ec701aab5658fd0f29da7b21855f8c75855b0af8f7a0f6111728c84ac72ecddd365a99840e1b8d5d24e82209bb1017159d4d424e3543b27efbe8dd76e002c308886857667d6ba17f1698f19c144617e225bb9283973934fda3270f4952488ae773f7e79c02009ba604692602d5da3988172f7a5ddbfa5fa5a1471995bc783a96c5f27d0b6d728cbc5ba991a08cd30ee1a9c88530fb1d09662ae5718fed61e0e9668669c75ee4d6eb793b329140c8e39c0e795c2a00ae8688e170883c3af91f0acd34574e9aa5eeb593aed1c623b73f00cc3fcb6807df486731a8e872bd46ad4ab1670fe76134c9eb4de7cf3ded7a7e6bd386d60cfe162b9e5d8f80d1fc9a549afc34f05ee2ca328a367949f375bb2183ecd7f31545cc87b6b19ce3e834d3a98156a71db69484e2213f15b750bb5e9c8e96ebc33abae12c269931dcf380d1f4a861eb4c768b93e751371b9ae211f210d381883976c5c587e9a6e674039a4b486c9fecae2025908cf05c7c88229abefa06a58a558c5fef9908fb546fad3489fb25b4ec62a75b0bc2aa2996bbbf89f6244f9ecbfd2e1e584a38b47c40f169c1612079ac83bfd8995c4154b440e5b02bf803a416ff3444f2e4b7398adcc8df331a210cc4fae101d06f533987fa59293e365361dfd6769b5a71fded46c7a360b5ddc2e8b16f76e5f0b67d696eeb0f9a01543f3aecb9c5b03b55da78e86adc59c8d57479ef9ae4bb86be4734e7587c023eefd7779180c316d4ae6fce63dc11c223c0f02fcf5190f336cab5b4845f050b6ef2eceab22638d7902b58886267c6662fc68102bae8bc7c78c329529251a3fe26a2c047b7910b8c1f874d623b3c2339cbbfe872d63a2ddd230f1cf2d45dc5731fd52641fd99176f3b7ed4fe072cd45b24efc4b0313719c720833e9b6bbb57533a2c1314f2266c1e4ea4fe4f4a8b5cdc46b6ef00e953ef9b3b453a3651ab2c2c96b2b928e239a37c76bfed66d405a88bb0597ca427492f4fc173a944bb6f2515609bd48e1b61b2be163f0f16e845de5f6c309f2c4ac49a9bd198c48b76cd56cc529fbc5fe31e04e5f499589c00957e260dad493a2abdd8487fc6d846bd7d63ed4a80b5b9ce2acebafc60eb9f295e260c51628bc511bbf4fd623690ea63ec2b725921a9927bae4d2ccf5fb07ee995c03e03bcf2d6e89d93db406eaa765f55114edc3b3f3de37f86064f92d045b47d3c53ebf219850b01acb1451f0adc2ca5028aa9ba565481866c55acc76e5c96f0d1b3d933960cc42229ea6d7539a8408c239d4bb324c5fcf7fc2950fd8001d0f02d9f4e25117e80037b3798afcfbcb9b6727b42ca4a50c77cdd3a9e0daaaea571e73a58be0cd5b1260d1d99a9968bb3fed510e567ec8a0bfcc9142ab7b43c03255fa85bf65fc5bf2c7826716597b4dc4df791c7f511f848f08dbb9f7da34f5c998a0f8a02892a243995842f5259175436c03985c5888ac4f4a2e6b9282048c1833226a51dcac1a988fcb099e4fa9a86494d58c010ae9214021f283c6760299b13c89cc8e4b1b98d412debb2c9a79fc7ed444b26a711b93b822cde0328c21530fbf2a075c190bb2eebc8d08ce1eb9cb37e784599db199d97a8c97824cbae8e48e781a82c4bba90f430739ec2d48488895313ccdb77301bdcac2e1465cd0cb3968ac941760795a06e67559d12b7d232854206be25c29fb9cd147dd07ba50536f694461b7d4e357724c9f6f9";
    private const string BigNewBlockSingleFrameDecrypted = "000a08c180000000000000000000000007f90a04f90a00f901f9a0ff483e972a04a9a62bb4b7d04ae403c615604e4090521ecc5bb7af67f71be09ca01dcc4de8dec75d7aab85b567b6ccd41ad312451b948a7413f0a142fd40d49347940000000000000000000000000000000000000000a056e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421a056e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421a056e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421b9010000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000830f424080833d090080830f424083010203a02ba5557a4c62a513c7e56d1bf13373e0da6bec016755483e91589fe1c6d212e28800000000000003e8f90800df80018252089400000000000000000000000000000000000000000180808080df80018252089400000000000000000000000000000000000000000180808080df80018252089400000000000000000000000000000000000000000180808080df80018252089400000000000000000000000000000000000000000180808080df80018252089400000000000000000000000000000000000000000180808080df80018252089400000000000000000000000000000000000000000180808080df80018252089400000000000000000000000000000000000000000180808080df80018252089400000000000000000000000000000000000000000180808080df80018252089400000000000000000000000000000000000000000180808080df80018252089400000000000000000000000000000000000000000180808080df80018252089400000000000000000000000000000000000000000180808080df80018252089400000000000000000000000000000000000000000180808080df80018252089400000000000000000000000000000000000000000180808080df80018252089400000000000000000000000000000000000000000180808080df80018252089400000000000000000000000000000000000000000180808080df80018252089400000000000000000000000000000000000000000180808080df80018252089400000000000000000000000000000000000000000180808080df80018252089400000000000000000000000000000000000000000180808080df80018252089400000000000000000000000000000000000000000180808080df80018252089400000000000000000000000000000000000000000180808080df80018252089400000000000000000000000000000000000000000180808080df80018252089400000000000000000000000000000000000000000180808080df80018252089400000000000000000000000000000000000000000180808080df80018252089400000000000000000000000000000000000000000180808080df80018252089400000000000000000000000000000000000000000180808080df80018252089400000000000000000000000000000000000000000180808080df80018252089400000000000000000000000000000000000000000180808080df80018252089400000000000000000000000000000000000000000180808080df80018252089400000000000000000000000000000000000000000180808080df80018252089400000000000000000000000000000000000000000180808080df80018252089400000000000000000000000000000000000000000180808080df80018252089400000000000000000000000000000000000000000180808080df80018252089400000000000000000000000000000000000000000180808080df80018252089400000000000000000000000000000000000000000180808080df80018252089400000000000000000000000000000000000000000180808080df80018252089400000000000000000000000000000000000000000180808080df80018252089400000000000000000000000000000000000000000180808080df80018252089400000000000000000000000000000000000000000180808080df80018252089400000000000000000000000000000000000000000180808080df80018252089400000000000000000000000000000000000000000180808080df80018252089400000000000000000000000000000000000000000180808080df80018252089400000000000000000000000000000000000000000180808080df80018252089400000000000000000000000000000000000000000180808080df80018252089400000000000000000000000000000000000000000180808080df80018252089400000000000000000000000000000000000000000180808080df80018252089400000000000000000000000000000000000000000180808080df80018252089400000000000000000000000000000000000000000180808080df80018252089400000000000000000000000000000000000000000180808080df80018252089400000000000000000000000000000000000000000180808080df80018252089400000000000000000000000000000000000000000180808080df80018252089400000000000000000000000000000000000000000180808080df80018252089400000000000000000000000000000000000000000180808080df80018252089400000000000000000000000000000000000000000180808080df80018252089400000000000000000000000000000000000000000180808080df80018252089400000000000000000000000000000000000000000180808080df80018252089400000000000000000000000000000000000000000180808080df80018252089400000000000000000000000000000000000000000180808080df80018252089400000000000000000000000000000000000000000180808080df80018252089400000000000000000000000000000000000000000180808080df80018252089400000000000000000000000000000000000000000180808080df80018252089400000000000000000000000000000000000000000180808080df80018252089400000000000000000000000000000000000000000180808080df80018252089400000000000000000000000000000000000000000180808080df80018252089400000000000000000000000000000000000000000180808080c0800000000000000000";

    private const int LongFrameSize = 48;
    private const int ShortFrameSize = 32;

    private byte[] _frame;
    private byte[] _shortFrame;
    private IFrameCipher _frameCipher;
    private FrameMacProcessor _macProcessor;

    [SetUp]
    public void Setup()
    {
        var (_, B) = NetTestVectors.GetSecretsPair();

        _frameCipher = new FrameCipher(B.AesSecret);
        _macProcessor = new FrameMacProcessor(TestItem.IgnoredPublicKey, B);

        _frame = new byte[16 + 16 + LongFrameSize + 16]; //  header | header MAC | packet type | data | padding | frame MAC
        _frame[2] = LongFrameSize - 15; // size (total - padding)

        _shortFrame = new byte[16 + 16 + 1 + ShortFrameSize + 15 + 16]; //  header | header MAC | packet type | data | padding | frame MAC
        _shortFrame[2] = ShortFrameSize - 15; // size (total - padding)
    }

    [TearDown]
    public void TearDown() => _macProcessor?.Dispose();

    [Test]
    public void Check_and_decrypt_block()
    {
        Test(ShortNewBlockSingleFrame, DeliverByteByByte, ShortNewBlockSingleFrameDecrypted);
    }

    [Test]
    public void Check_and_decrypt_big_frame_delivered_byte_by_byte()
    {
        Test(BigNewBlockSingleFrame, DeliverByteByByte, BigNewBlockSingleFrameDecrypted);
    }

    [Test]
    public void Check_and_decrypt_big_frame_delivered_all_at_once()
    {
        Test(BigNewBlockSingleFrame, DeliverAllAtOnce, BigNewBlockSingleFrameDecrypted);
    }

    [Test]
    public void Check_and_decrypt_big_frame_delivered_all_at_once_and_followed_by_a_corrupted_header()
    {
        Test(BigNewBlockSingleFrame, DeliverAllAtOnceFollowedByACorruptedHeader, BigNewBlockSingleFrameDecrypted);
    }

    [Test]
    public void Check_and_decrypt_big_frame_delivered_block_by_block()
    {
        Test(BigNewBlockSingleFrame, DeliverBlockByBlock, BigNewBlockSingleFrameDecrypted);
    }

    private void Test(string frame, Func<byte[], IByteBuffer, ZeroFrameDecoderTestWrapper, IByteBuffer> deliveryStrategy, string expectedOutput)
    {
        byte[] frameBytes = Bytes.FromHexString(frame);
        IByteBuffer input = ReferenceCountUtil.ReleaseLater(Unpooled.Buffer(256));
        ZeroFrameDecoderTestWrapper zeroFrameDecoderTestWrapper = new(_frameCipher, _macProcessor);

        IByteBuffer result = ReferenceCountUtil.ReleaseLater(deliveryStrategy(frameBytes, input, zeroFrameDecoderTestWrapper));
        Assert.That(result, Is.Not.Null, "did not decode frame");

        byte[] resultBytes = new byte[result.ReadableBytes];
        result.ReadBytes(resultBytes);
        TestContext.Out.WriteLine(resultBytes.ToHexString());
        string expected = expectedOutput;
        TestContext.Out.WriteLine(resultBytes.ToHexString());
        Assert.That(resultBytes.ToHexString(), Is.EqualTo(expected));
        Assert.That(input.WriterIndex, Is.EqualTo(input.ReaderIndex), "reader index == writer index");
    }

    private static IByteBuffer DeliverAllAtOnce(byte[] frame, IByteBuffer input, ZeroFrameDecoderTestWrapper zeroFrameDecoderTestWrapper)
    {
        input.WriteBytes(frame);
        return zeroFrameDecoderTestWrapper.Decode(input);
    }

    private static IByteBuffer DeliverAllAtOnceFollowedByACorruptedHeader(byte[] frame, IByteBuffer input, ZeroFrameDecoderTestWrapper zeroFrameDecoderTestWrapper)
    {
        byte[] corruptedHeader = new byte[32];
        input.WriteBytes(Bytes.Concat(frame, corruptedHeader));
        return zeroFrameDecoderTestWrapper.Decode(input, false);
    }

    private static IByteBuffer DeliverByteByByte(byte[] frame, IByteBuffer input, ZeroFrameDecoderTestWrapper zeroFrameDecoderTestWrapper)
    {
        IByteBuffer result = null;
        for (int i = 0; i < frame.Length; i++)
        {
            input.WriteByte(frame[i]);
            result = zeroFrameDecoderTestWrapper.Decode(input);
            if (result is not null)
            {
                break;
            }
        }

        return result;
    }

    private static IByteBuffer DeliverBlockByBlock(byte[] frame, IByteBuffer input, ZeroFrameDecoderTestWrapper zeroFrameDecoderTestWrapper)
    {
        IByteBuffer result = null;
        for (int i = 0; i < frame.Length; i += 16)
        {
            input.WriteBytes(frame.Slice(i, 16));
            result = zeroFrameDecoderTestWrapper.Decode(input);
            if (result is not null)
            {
                break;
            }
        }

        return result;
    }
}
