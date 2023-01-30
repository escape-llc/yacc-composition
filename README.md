# Yet Another Chart Component (YACC) on Composition Layer

A whole lot has happened since we built YACC, in particular the Composition Layer.  We are starting over, taking lessons learned and relevant source code over to this repository.

> Links will eventually work, stay tuned...

## Internals
Lots of stuff is changing; we are re-thinking all aspects of the internals.  TL;DR you are warned!

### Event Bus
One thing changing in this iteration is we are going with an Event Bus for inter-component communication, and this helps remove some bookkeeping and opens up who can see what information during processing.  It should be no surprise we are using our own Nuget package for the Event Bus implementation.  As a consequence, many of the `IRequireXXX` interfaces become messages on the bus.

### Render Pipeline
Driving the render pipeline is much simpler, and consists of broadcasting a series of `Phase_xxx` messages that mostly match with the legacy YACC phases.  Now that components can "listen in" on messages, tasks like extent management became easier.  There is no longer any explicit "phase" to pull extents from axes; these are broadcast on the bus for anyone interested.  Series and decorations broadcast their extents, which get picked up by axes.  Axes in turn broadcast updated extents, so everyone is updated immediately and with the proper timing.

### `DispatcherQueue` and Command Port
While we like the `DataSource` concept, tracking some VM's `ObservableCollection` just doesn't provide enough context to pull off the kinds of animated elements we were looking for in the legacy YACC.  To combat this, we are doing away with `DataSource` taking an `IEnumerable` and swiching to a "command port" pattern, which is based on the legacy `RefreshRequest` dependency property.

Instead of using an `int` value, we are replacing it with full blown "commands", e.g. "fill chart with this list of items" or "perform sliding window".  We leverage the dependency property callback machinery to take this and submit units of work to the render pipeline via `DispatcherQueue` which has the benefit of being non-`async` (unlike `DispatcherCore.RunAsync`).  So now, to make the chart update, simply assign a command object to the `CommandPort` and let the magic happen!  Having units of work also confines data updates to stable representations of the chart internals (only relevant with multiple `DataSource`s).

### Incremental Updates
This change in turn leads to a revamp of how individual series manage their visual tree, because now there is more context in which to coordinate all the visual activities together.  This led to more standardization in the algorithm for incmental updates.

### Linear Algebra
Linear Algebra, the basis of how YACC renders, is super-clean in the Composition Layer; the "outer" container takes the P matrix, and all of its children take the M matrix.  This removes some situations that required combining the matrices and manipulating `Canvas` offsets in PX units (gak).  Transforms-only render now only has to adjust a single Composition Object with the new P matrix.

### XAML Bummer
One downside is Composition objects do not participate in the `Style` system all the XAML elements use.  We are responding to that by IoC; you now provide an "element factory" to produce Composition objects, and you may style objects as necessary.  We provide a collection of "default" element factories to get you running.

Providing your own element factory opens the door for attaching composition animations to chart elements.  Implicit enter and leave, `Opacity`, `Offset` and other animations are possible.  Changing "style" on composition elements can also take the form of animations, e.g. a `Color` animation.

The "text" layer still uses the venerable XAML layer, and that will continue to function the same.  We looked into using composition layer text, but we are passing for the moment, mostly because of the ease of styling XAML `TextBlock`.  The main difference in handling of `TextBlock` is we are using the `Translation` property and not `Canvas.Offset`.

### Item State
The `ItemState` layer is revamped; there was too much hard-coding to the "horizontal" orientation, and this made building transforms more confusing, as it was difficult to tell when the coordinates "crossed over" to being physical coordinates (and not components).  In keeping with terminology of Linear Algebra, everything is renamed in terms of "Components" that have no regard for the (cartesian/radial) axis they are assigned to.  This makes supporting multiple chart orientations easier and less confusing to the programmer.  Components are mapped to axes as directed by the axis orientations of the series.

### Placement
This feature is very nice conceptually, but the implementation suffered from similar "coordinate confusion" and issues obtaining the matrices needed to get to the PX coordinates for `TextBlock`.  The `ValueLabels` component was a hodgepodge of different placement algorithms.

Now that the timing of transforms is more organized, we have inverted this feature such that the source of placement provides all the required transforms via a `LayoutSession` which a component like `ValueLabels` can request and use to get PX coordinates with little effort.  The source is responsible for the value and `PlacementOffset`; the "placer" is only responsible for the `LabelOffset`.

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
