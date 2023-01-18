# Yet Another Chart Component (YACC) on Composition Layer

A whole lot has happened since we built YACC, in particular the Composition Layer.  We are starting over, taking lessons learned and relevant source code over to this repository.

> Links will eventually work, stay tuned...

## Internals
One thing changing in this iteration is we are going with an Event Bus for inter-component communication, and this helps remove some bookkeeping and opens up who can see what information during processing.  It should be no surprise we are using our own Nuget package for the Event Bus implementation.  As a consequence, many of the `IRequireXXX` interfaces become messages on the bus.

Another interesting consequence is that the Linear Algebra, the basis of how YACC renders, works out much nicer in the Composition Layer; the "outer" container takes the P matrix, and all of its children take the M matrix.  This is much cleaner and removes some situations that required combining the matrices and manipulating `Canvas` offsets in PX units (gak).  Transforms-only render now only has to adjust a single Composition Object with the new P matrix.

Axis extent transfer also works out nicely via the Event Bus, and there is no longer any explicit "phase" to pull extents from axes, as these are now broadcast on the bus for anyone interested.  Conversely, series and decorations also broadcast their extents, which get picked up by axes.  Axes in turn broadcast updated extents, so everyone is updated immediately and with the proper timing.

One downside is Composition objects do not participate in the `Style` system all the XAML elements use.  We are responding to that by IoC; you now provide an "element factory" to produce Composition objects, and you may style objects as necessary.  We provide a collection of "default" element factories to get you running.

The "text" layer still uses the venerable XAML layer, and that will continue to function the same.

The `ItemState` layer is revamped; there was too much hard-coding to the "horizontal" orientation, and this made building transforms more confusing, as it was difficult to tell when the coordinates "crossed over" to being physical coordinates (and not components).  In keeping with terminology of Linear Algebra, everything is renamed in terms of "Components" that have no regard for the (cartesian/radial) axis they are assigned to.  This makes supporting multiple chart orientations easier and less confusing to the programmer.  Components are mapped to axes as directed by the axis orientations of the series.

## Demo It!
The demo application in the solution is available in [Windows Store](https://www.microsoft.com/store/apps/9P9XC6Z7R3BW) so you don't have to build it from source.

> Still points to original YACC demo app.

## Get It!
From *Package Manager Console*:
```
   PM> Install-Package eScapeLLC.UWP.Charts.Composition
```
[![NuGet version](https://badge.fury.io/nu/eScapeLLC.UWP.Charts.Composition.svg)](https://badge.fury.io/nu/eScapeLLC.UWP.Charts.Composition)

[Package page on nuget.org](https://www.nuget.org/packages/eScapeLLC.UWP.Charts.Composition/)

> Not published just yet...
