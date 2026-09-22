document.querySelectorAll('.reveal').forEach((element) => {
  const observer = new IntersectionObserver((entries, instance) => {
    entries.forEach((entry) => {
      if (!entry.isIntersecting) return;
      entry.target.classList.add('visible');
      instance.unobserve(entry.target);
    });
  }, { threshold: 0.12 });
  observer.observe(element);
});

document.querySelectorAll('[data-copy]').forEach((button) => {
  button.addEventListener('click', async () => {
    const value = button.getAttribute('data-copy');
    const label = button.querySelector('.copy-label');
    try {
      await navigator.clipboard.writeText(value);
      label.textContent = 'Copied';
      window.setTimeout(() => { label.textContent = 'Copy'; }, 1400);
    } catch {
      label.textContent = 'Select';
    }
  });
});