// Run in a browser on the Vite dev server:
// await import('/tests/homeLayout.browser.ts').then(module => module.checkHomeLayout())
export async function checkHomeLayout() {
  const results = []
  for (const width of [320, 768, 1024, 1440, 1920]) {
    const frame = document.createElement("iframe")
    frame.style.cssText = `position:fixed;left:0;top:0;width:${width}px;height:1000px;border:0;z-index:99999;background:white`
    const loaded = new Promise<void>((resolve) => { frame.onload = () => resolve() })
    frame.src = "/?district=Qu%E1%BA%ADn+7"
    document.body.append(frame)
    try {
      await loaded
      const doc = frame.contentDocument!
      const view = frame.contentWindow!
      await doc.fonts.ready
      await new Promise<void>((resolve) => view.requestAnimationFrame(() => resolve()))
      const viewport = doc.documentElement.clientWidth
      const gutter = width >= 1536 ? 48 : width >= 1024 ? 32 : width >= 640 ? 24 : 16
      const measurements = ["#tim-phong form", "#khu-vuc form"].map((selector) => {
        const element = doc.querySelector(selector)
        const rect = element?.getBoundingClientRect()
        return {
          selector,
          left: rect?.left,
          right: rect ? viewport - rect.right : undefined,
          pass: !!rect && Math.abs(rect.left - gutter) <= 1 && Math.abs(viewport - rect.right - gutter) <= 1,
        }
      })
      const aligned = ["header > div", "#gioi-thieu > div", "footer > div"].every((selector) => {
        const element = doc.querySelector(selector)
        if (!element) return false
        const rect = element.getBoundingClientRect()
        const style = view.getComputedStyle(element)
        return Math.abs(rect.left + parseFloat(style.paddingLeft) - gutter) <= 1 &&
          Math.abs(viewport - rect.right + parseFloat(style.paddingRight) - gutter) <= 1
      })
      const overflow = doc.documentElement.scrollWidth > viewport + 1
      const headerLinks = [...doc.querySelectorAll<HTMLAnchorElement>("header a")]
        .map((element) => element.getBoundingClientRect()).filter((rect) => rect.width > 0)
      const overlap = headerLinks.some((first, index) => headerLinks.slice(index + 1).some((second) =>
        first.left < second.right && first.right > second.left && first.top < second.bottom && first.bottom > second.top,
      ))
      let defaultWidthPreserved = true
      if (width === 1920) {
        doc.querySelector(".home-page")?.classList.remove("home-page")
        const defaultWidth = doc.querySelector("header > div")?.getBoundingClientRect().width
        defaultWidthPreserved = defaultWidth !== undefined && Math.abs(defaultWidth - 1180) <= 1
      }
      results.push({ width, viewport, measurements, aligned, overflow, overlap,
        defaultWidthPreserved,
        pass: measurements.every((item) => item.pass) && aligned && !overflow && !overlap && defaultWidthPreserved,
      })
    } finally {
      frame.remove()
    }
  }
  return { pass: results.every((result) => result.pass), results }
}
