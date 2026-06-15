document.addEventListener('DOMContentLoaded', () => {
    const navToggleBtn = document.getElementById('nav-toggle-btn');
    const navLinks = document.getElementById('nav-links');

    if (navToggleBtn && navLinks) {
        navToggleBtn.addEventListener('click', () => {
            const expanded = navToggleBtn.getAttribute('aria-expanded') === 'true';
            navToggleBtn.setAttribute('aria-expanded', !expanded);
            navLinks.classList.toggle('active');
            
            navToggleBtn.classList.toggle('open');
        });

        navLinks.querySelectorAll('a').forEach(link => {
            link.addEventListener('click', () => {
                navToggleBtn.setAttribute('aria-expanded', 'false');
                navLinks.classList.remove('active');
                navToggleBtn.classList.remove('open');
            });
        });
    }



    const canvas = document.getElementById('hero-canvas');
    if (canvas) {
        const ctx = canvas.getContext('2d');
        let particles = [];
        let animationFrameId;

        const resizeCanvas = () => {
            canvas.width = canvas.offsetWidth;
            canvas.height = canvas.offsetHeight;
        };
        resizeCanvas();
        window.addEventListener('resize', resizeCanvas);

        class Particle {
            constructor() {
                this.x = Math.random() * canvas.width;
                this.y = Math.random() * canvas.height;
                this.vx = (Math.random() - 0.5) * 0.4;
                this.vy = (Math.random() - 0.5) * 0.4;
                this.radius = Math.random() * 2 + 1;
            }

            update() {
                this.x += this.vx;
                this.y += this.vy;

                if (this.x < 0 || this.x > canvas.width) this.vx *= -1;
                if (this.y < 0 || this.y > canvas.height) this.vy *= -1;
            }

            draw() {
                ctx.fillStyle = 'rgba(96, 165, 250, 0.4)';
                ctx.beginPath();
                ctx.arc(this.x, this.y, this.radius, 0, Math.PI * 2);
                ctx.fill();
            }
        }

        const initParticles = () => {
            particles = [];
            const count = Math.min(60, Math.floor(canvas.width / 20));
            for (let i = 0; i < count; i++) {
                particles.push(new Particle());
            }
        };
        initParticles();

        const drawConnections = () => {
            const maxDistance = 120;
            
            for (let i = 0; i < particles.length; i++) {
                for (let j = i + 1; j < particles.length; j++) {
                    const dx = particles[i].x - particles[j].x;
                    const dy = particles[i].y - particles[j].y;
                    const dist = Math.sqrt(dx * dx + dy * dy);

                    if (dist < maxDistance) {
                        const opacity = (1 - dist / maxDistance) * 0.15;
                        ctx.strokeStyle = `rgba(96, 165, 250, ${opacity})`;
                        ctx.lineWidth = 0.8;
                        ctx.beginPath();
                        ctx.moveTo(particles[i].x, particles[i].y);
                        ctx.lineTo(particles[j].x, particles[j].y);
                        ctx.stroke();
                    }
                }
            }
        };

        const animate = () => {
            ctx.clearRect(0, 0, canvas.width, canvas.height);
            
            particles.forEach(p => {
                p.update();
                p.draw();
            });
            drawConnections();

            animationFrameId = requestAnimationFrame(animate);
        };
        animate();
    }

    const mockCpuTemp = document.getElementById('mock-cpu-temp');
    const mockGpuTemp = document.getElementById('mock-gpu-temp');
    const mockFanSpeed = document.getElementById('mock-fan-speed');
    const mockPowerState = document.getElementById('mock-power-state');

    const mockupState = {
        powerMode: 'balanced', 
        fanMode: 'auto',       
        displayMode: 'maxhz',  
        targetCpu: 44,
        targetGpu: 39,
        baseFanRpm: 2100,      
        currentCpu: 44,
        currentGpu: 39,
        currentFanRpm: 2100
    };

    setInterval(() => {
        // Simple noise
        const cpuNoise = (Math.random() - 0.5) * 2;
        const gpuNoise = (Math.random() - 0.5) * 1.5;
        
        mockupState.currentCpu = Math.round(mockupState.targetCpu + cpuNoise);
        mockupState.currentGpu = Math.round(mockupState.targetGpu + gpuNoise);

        mockCpuTemp.textContent = `${mockupState.currentCpu}°C`;
        if (mockupState.targetGpu && mockupState.targetGpu > 0) {
            mockGpuTemp.textContent = `${mockupState.currentGpu}°C`;
        } else {
            mockGpuTemp.textContent = `--°C`;
        }
    }, 1000);

    const powerButtons = document.querySelectorAll('[data-row="power-mode"] .mock-btn');
    powerButtons.forEach(btn => {
        btn.addEventListener('click', () => {
            powerButtons.forEach(b => b.classList.remove('active'));
            btn.classList.add('active');

            mockupState.powerMode = btn.dataset.key;
            mockupState.targetCpu = parseInt(btn.dataset.cpu, 10);
            mockupState.targetGpu = parseInt(btn.dataset.gpu, 10);
            
            mockPowerState.textContent = btn.dataset.val;
            mockPowerState.className = 'tel-val';
        });
    });

    const fanButtons = document.querySelectorAll('[data-row="fan-mode"] .mock-btn');
    fanButtons.forEach(btn => {
        btn.addEventListener('click', () => {
            fanButtons.forEach(b => b.classList.remove('active'));
            btn.classList.add('active');
            mockupState.fanMode = btn.dataset.key;
            mockFanSpeed.textContent = btn.dataset.val;
        });
    });

    const displayButtons = document.querySelectorAll('[data-row="display-mode"] .mock-btn');
    displayButtons.forEach(btn => {
        btn.addEventListener('click', () => {
            displayButtons.forEach(b => b.classList.remove('active'));
            btn.classList.add('active');
            mockupState.displayMode = btn.dataset.key;
        });
    });

    const brightnessSlider = document.getElementById('slider-brightness');
    const brightnessVal = document.getElementById('val-brightness');
    if (brightnessSlider && brightnessVal) {
        brightnessSlider.addEventListener('input', (e) => {
            brightnessVal.textContent = `${e.target.value}%`;
        });
    }

    const speedSlider = document.getElementById('slider-speed');
    const speedVal = document.getElementById('val-speed');
    if (speedSlider && speedVal) {
        speedSlider.addEventListener('input', (e) => {
            speedVal.textContent = `${e.target.value}%`;
        });
    }

    const btnColorPick = document.getElementById('btn-color-pick');
    const colorSwatch = document.getElementById('picker-color-swatch');
    
    const colors = [
        '#00c8a0', 
        '#ff3b30', 
        '#ff9500', 
        '#ffcc00', 
        '#34c759', 
        '#007aff', 
        '#5856d6', 
        '#af52de'  
    ];
    let currentColorIdx = 0;

    if (btnColorPick && colorSwatch) {
        btnColorPick.addEventListener('click', () => {
            currentColorIdx = (currentColorIdx + 1) % colors.length;
            const nextColor = colors[currentColorIdx];
            colorSwatch.style.backgroundColor = nextColor;
            colorSwatch.style.boxShadow = `0 0 6px ${nextColor}`;
        });
    }
});
