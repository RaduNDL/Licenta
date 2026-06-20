(function () {
    'use strict';

    const searchInput = document.getElementById('reviewSearch');
    const ratingFilter = document.getElementById('reviewRatingFilter');

    
    function getActiveCards() {
        const activePane = document.querySelector('.tab-pane.active.show');
        if (!activePane) return [];
        return Array.from(activePane.querySelectorAll('.review-card'));
    }

    function applyFilters() {
        const cards = getActiveCards();
        const searchText = (searchInput?.value || '').toLowerCase().trim();
        const ratingValue = parseInt(ratingFilter?.value || '0', 10);

        cards.forEach(card => {
            const rating = parseInt(card.dataset.rating || '0', 10);
            const hay = (card.dataset.search || '').toLowerCase();

            const matchesRating = ratingValue === 0 || rating === ratingValue;
            const matchesSearch = searchText === '' || hay.includes(searchText);

            card.style.display = (matchesRating && matchesSearch) ? '' : 'none';
        });
    }

    if (searchInput) {
        let t;
        searchInput.addEventListener('input', () => {
            clearTimeout(t);
            t = setTimeout(applyFilters, 160);
        });
    }
    if (ratingFilter) ratingFilter.addEventListener('change', applyFilters);

    document.querySelectorAll('[data-bs-toggle="tab"]').forEach(btn => {
        btn.addEventListener('shown.bs.tab', () => applyFilters());
    });

    const form = document.getElementById('reviewForm');
    if (!form) return;

    const formTitle = document.getElementById('formPanelTitle');
    const btnCancelEdit = document.getElementById('btnCancelEdit');
    const submitBtn = document.getElementById('reviewSubmitBtn');
    const submitSlot = submitBtn ? submitBtn.querySelector('[data-slot="submit"]') : null;

    const inputId = document.getElementById('Input_Id');
    const inputDoctorId = document.getElementById('Input_DoctorId');
    const inputTitle = document.getElementById('Input_Title');
    const inputComment = document.getElementById('Input_Comment');

    const targetWrap = document.getElementById('targetChoiceWrap');
    const doctorSelectWrap = document.getElementById('doctorSelectWrap');
    const targetDoctorRadio = document.getElementById('target-doctor');
    const targetAppRadio = document.getElementById('target-app');

    const deleteForm = document.getElementById('deleteReviewForm');
    const deleteId = document.getElementById('deleteReviewId');

    function setRating(rating) {
        const v = parseInt(rating || '5', 10);
        const el = document.getElementById(`rating-${v}`);
        if (el) el.checked = true;
    }

    function setTarget(target, doctorId) {
        const isDoctor = String(target).toLowerCase() === 'doctor';

        if (targetDoctorRadio) targetDoctorRadio.checked = isDoctor;
        if (targetAppRadio) targetAppRadio.checked = !isDoctor;

        if (doctorSelectWrap) doctorSelectWrap.classList.toggle('d-none', !isDoctor);

        if (inputDoctorId) {
            inputDoctorId.value = isDoctor ? (doctorId || '') : '';
        }
    }

    function enterEditMode(data) {
        if (targetWrap) targetWrap.classList.add('d-none');
        if (doctorSelectWrap) doctorSelectWrap.classList.add('d-none'); 

        form.action = '?handler=Edit';
        if (formTitle) formTitle.textContent = 'Edit Review';
        if (submitSlot) submitSlot.textContent = 'Save Changes';
        if (btnCancelEdit) btnCancelEdit.classList.remove('d-none');

        if (inputId) inputId.value = data.id || '';
        setTarget(data.target, data.doctorId);
        setRating(data.rating);

        if (inputTitle) inputTitle.value = data.title || '';
        if (inputComment) inputComment.value = data.comment || '';

        form.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }

    function exitEditMode() {
        form.action = '?handler=Create';
        if (formTitle) formTitle.textContent = 'Write a Review';
        if (submitSlot) submitSlot.textContent = 'Submit Feedback';
        if (btnCancelEdit) btnCancelEdit.classList.add('d-none');

        if (inputId) inputId.value = '';
        if (inputTitle) inputTitle.value = '';
        if (inputComment) inputComment.value = '';
        setRating(5);

        if (targetWrap) targetWrap.classList.remove('d-none');

        const isDoctor = !!(targetDoctorRadio && targetDoctorRadio.checked);
        if (doctorSelectWrap) doctorSelectWrap.classList.toggle('d-none', !isDoctor);
        if (inputDoctorId && !isDoctor) inputDoctorId.value = '';
    }

    function refreshCreateModeTargetUI() {
        if (!targetDoctorRadio || !targetAppRadio) return;

        const isEdit = (inputId && inputId.value && inputId.value !== '');
        if (isEdit) return;

        const isDoctor = targetDoctorRadio.checked;
        if (doctorSelectWrap) doctorSelectWrap.classList.toggle('d-none', !isDoctor);
        if (inputDoctorId && !isDoctor) inputDoctorId.value = '';
    }

    if (targetDoctorRadio) targetDoctorRadio.addEventListener('change', refreshCreateModeTargetUI);
    if (targetAppRadio) targetAppRadio.addEventListener('change', refreshCreateModeTargetUI);

    if (btnCancelEdit) btnCancelEdit.addEventListener('click', exitEditMode);

    document.querySelectorAll('.js-edit-review').forEach(btn => {
        btn.addEventListener('click', () => {
            const data = {
                id: btn.getAttribute('data-id'),
                target: btn.getAttribute('data-target'),
                doctorId: btn.getAttribute('data-doctor-id'),
                rating: btn.getAttribute('data-rating'),
                title: btn.getAttribute('data-title'),
                comment: btn.getAttribute('data-comment')
            };
            enterEditMode(data);
        });
    });


    document.querySelectorAll('.js-delete-review').forEach(btn => {
        btn.addEventListener('click', () => {
            const id = btn.getAttribute('data-id');
            if (!id || !deleteForm || !deleteId) return;

            const ok = window.confirm('Are you sure you want to delete this review?');
            if (!ok) return;

            deleteId.value = id;
            deleteForm.submit();
        });
    });

    refreshCreateModeTargetUI();
    applyFilters();

    const charCount = document.getElementById('charCount');
    function updateCharCount() {
        if (!charCount || !inputComment) return;
        charCount.textContent = String((inputComment.value || '').length);
    }
    if (inputComment) {
        inputComment.addEventListener('input', updateCharCount);
        updateCharCount();
    }
})();