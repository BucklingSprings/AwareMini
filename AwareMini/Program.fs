namespace BucklingSprings.Aware.AddOns.Mini

open System
open System.Drawing
open System.Drawing.Text
open System.Windows.Forms

open Microsoft.VisualBasic.ApplicationServices
open Microsoft.Win32

open Nessos.UnionArgParser

module Program =

    type Arguments =
        | Task_Name of string
        | Percentage_Done of int
        | Minutes of int
        | Word_Count of int
        | More_Or_Less of string
    with
        interface IArgParserTemplate with
            member s.Usage = 
                match s with
                    | Arguments.Task_Name _ -> "Name of the current task."
                    | Arguments.Percentage_Done _ -> "Percentage done as an integral number petween 0 and 100."
                    | Arguments.Minutes _ -> "Minutes spent on task so far."
                    | Arguments.Word_Count _ -> "Number of words typed so far."
                    | Arguments.More_Or_Less _ -> "More, Less or Neutral. Should you be doing More or Less of this task."
                    

    type MoreOrLess =
        | MoreOf
        | LessOf
        | Neutral
    
    type Progress = {
        taskName : string
        percentage : float32
        moreOrless : MoreOrLess
        wordCount : int
        minutes : int
        refreshedAt : DateTime
    }

    let progressFromCommandLine (argv : string[]) =
        let moreOrLessParser s =
            if "more".Equals(s, StringComparison.CurrentCultureIgnoreCase) then
                MoreOrLess.MoreOf
            elif "less".Equals(s, StringComparison.CurrentCultureIgnoreCase) then
                MoreOrLess.LessOf
            else
                MoreOrLess.Neutral
        let parser = UnionArgParser.Create<Arguments>()
        let results = parser.Parse(argv)
        let percentageDone = (float32 (results.GetResult (<@ Percentage_Done @>, 0))) / (100.0f)
        let moreOrLess = 
                    if results.Contains(<@ Arguments.More_Or_Less @>) then
                        results.PostProcessResult(<@ Arguments.More_Or_Less @>, moreOrLessParser)
                    else
                        MoreOrLess.Neutral

        {
            Progress.minutes = results.GetResult (<@ Minutes @>, 0)
            Progress.moreOrless = moreOrLess
            Progress.percentage = percentageDone
            Progress.taskName = results.GetResult (<@ Task_Name @>, "--")
            Progress.wordCount = results.GetResult (<@ Word_Count @>, 0)
            Progress.refreshedAt = DateTime.Now
         }

    let formatMinutes ms =
        let ts = TimeSpan.FromMinutes(float ms)
        sprintf "%02d:%02d" (int ts.Hours) (int ts.Minutes)

    type FloatWindow(p : Progress) as f =
        inherit Form()
        let fonts = new PrivateFontCollection()
        let mutable progress = p
        let regKey = "HKEY_CURRENT_USER\Software\FloatAwareAddOn"
        let panel = new Panel()
        let restorePosition (c : Control) =
            let screen = Screen.FromControl(f).Bounds
            let savedTop = Registry.GetValue(regKey, "Top", 0)
            let savedLeft = Registry.GetValue(regKey, "Left", 0)
            f.Top <- if savedTop <> null then savedTop :?> int else 0
            if f.Top > screen.Height then
               f.Top <- screen.Height - f.Height 
            f.Left <- if savedLeft <> null then savedLeft :?> int else 0
            if f.Left > screen.Width then
               f.Left <- screen.Width - f.Width 

        let savePosition (c : Control) =
            Registry.SetValue(regKey, "Top", c.Top)
            Registry.SetValue(regKey, "Left", c.Left)
        do
            f.FormBorderStyle <- FormBorderStyle.Fixed3D
            f.MaximizeBox <- false
            f.TopMost <- true
            f.StartPosition <- FormStartPosition.Manual
            f.Width <- 400
            f.Height <- 100
            f.BackColor <- Color.LightSlateGray
            f.ShowInTaskbar <- false
            f.Opacity <- 0.7
            f.ShowIcon <- false

            restorePosition f
            f.Move.Add(fun _ -> savePosition f)
            
            let cs = f.ClientSize

            panel.Anchor <- AnchorStyles.None
            panel.Height <- cs.Height
            panel.Width <- int cs.Width
            panel.Top <- 0
            panel.Left <- 0

            fonts.AddFontFile("Lato-Reg.ttf")

     
            f.Controls.Add(panel)

            panel.Paint.Add(fun _ ->
                let green = Color.FromArgb(191, 203, 67)
                let red = Color.FromArgb(223, 107, 50)
                let blue = Color.FromArgb(58, 173, 217)
                let s = sprintf "%s             %d words" (formatMinutes progress.minutes) progress.wordCount
                use g = panel.CreateGraphics()
                let p = Pens.Red
                let ff = fonts.Families.[0]
                let f = new Font(ff, 20.0f)
                let b =
                    new SolidBrush(match progress.moreOrless with
                                    | MoreOrLess.MoreOf -> green
                                    | MoreOrLess.LessOf -> red
                                    | MoreOrLess.Neutral -> blue)

                g.FillRectangle(b,0,0,int(progress.percentage * (float32 cs.Width)),cs.Height)
                let strSz = g.MeasureString(s, f)
                g.DrawString(s, f, Brushes.White, 10.0f,(float32 cs.Height - strSz.Height) / 2.0f))


            f.ShowInformation(p)
        member x.ShowInformation (p : Progress) =
            f.Text <- sprintf "%s - %s" p.taskName (p.refreshedAt.ToShortTimeString())
            progress <- p
            panel.Invalidate()
            
            

    type Application(argv : string[]) as a =
        inherit WindowsFormsApplicationBase()

        do
            a.IsSingleInstance <- true
            a.StartupNextInstance.Add(fun ea -> 
                let now = DateTimeOffset.Now
                let argv : string[] = Array.zeroCreate(ea.CommandLine.Count)
                ea.CommandLine.CopyTo(argv, 0)
                ea.BringToForeground <- false
                a.ShowInformation(progressFromCommandLine argv))


        override x.OnCreateMainForm () =
            x.MainForm <- new FloatWindow(progressFromCommandLine(argv))
        member x.ShowInformation (p : Progress) =
            (x.MainForm :?> FloatWindow).ShowInformation p

    [<EntryPoint>]
    [<STAThread>]
    let main argv =
        let a = Application(argv)
        a.Run(argv)
        0
        

