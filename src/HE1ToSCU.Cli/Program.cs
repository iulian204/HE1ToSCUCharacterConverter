// See https://aka.ms/new-console-template for more information
using HE1ToSCU.Core;

if(args.Length<2)
{
    Console.WriteLine("HE1ToSCU.Cli <model> <new-output-folder> [--game generations|unleashed] [--generate-mouth-l] [--skeleton file.hkx] [--animation file.hkx]... [--animation-folder folder] [--acorn editor.exe]");
    return 2;
}
try
{
    string? skeleton=null,editor=null;var animations=new List<string>();var game=SourceGame.SonicGenerations;bool generateMouthL=false;
    for(int i=2;i<args.Length;i++)
    {
        if(args[i]=="--generate-mouth-l") {generateMouthL=true;continue;}
        if(i+1>=args.Length)throw new ArgumentException($"Missing value after {args[i]}.");
        switch(args[i])
        {
            case "--skeleton":skeleton=args[++i];break;
            case "--animation":animations.Add(args[++i]);break;
            case "--animation-folder":animations.AddRange(Directory.GetFiles(args[++i],"*.anm.hkx").Order(StringComparer.OrdinalIgnoreCase));break;
            case "--game":game=args[++i].ToLowerInvariant() switch {"generations"=>SourceGame.SonicGenerations,"unleashed"=>SourceGame.SonicUnleashed,_=>throw new ArgumentException("Game must be generations or unleashed.")};break;
            case "--acorn":editor=args[++i];break;
            default:throw new ArgumentException($"Unknown option {args[i]}.");
        }
    }
    using var cancellation=new CancellationTokenSource();
    Console.CancelKeyPress+=(_,e)=>{e.Cancel=true;cancellation.Cancel();};
    var progress=new InlineProgress(p=>Console.WriteLine($"[{p.Percent,3}%] {p.Message}"));
    await Task.Run(()=>new ConversionService().ConvertAsync(new ConversionRequest(args[0],skeleton,animations,args[1],editor,game,generateMouthL),progress,cancellation.Token));
    return 0;
}
catch(Exception e){Console.Error.WriteLine(e.ToString());return 1;}

sealed class InlineProgress(Action<ConversionProgress> report):IProgress<ConversionProgress>
{
    public void Report(ConversionProgress value)=>report(value);
}
